using Codeless.DynamicType;
using Codeless.Extensions;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Script.Serialization;

namespace Codeless {
  public class TemplateOptions {
    private readonly Hashtable globals = new Hashtable();

    public TemplateOptions() { }

    public Hashtable Globals { get { return globals; } }
  }

  public class TemplateParseException : Exception {
    public TemplateParseException(string message) : base(message) { }
  }

  public class Template {
    public static string Evaluate(string str, object obj) {
      return Evaluate(str, obj, new TemplateOptions());
    }

    public static string Evaluate(string str, object obj, TemplateOptions options) {
      return new Template(str, obj, options).Evaluate();
    }

    #region Evaluator
    private static readonly ConcurrentDictionary<string, IList<Token>> ParseResultCache = new ConcurrentDictionary<string, IList<Token>>();
    private readonly StringBuilder output = new StringBuilder();
    private readonly Stack<DynamicValue> objStack = new Stack<DynamicValue>();
    private readonly Stack<Queue<DynamicValue>> iteratorStack = new Stack<Queue<DynamicValue>>();
    private readonly JavaScriptSerializer json = new JavaScriptSerializer();
    private readonly DynamicValue globals;
    private readonly DynamicValue pipes;
    private readonly TemplateOptions options;
    private readonly string str;

    private Template(string str, object data, TemplateOptions options) {
      this.str = str;
      this.options = options;
      this.globals = new DynamicValue(options.Globals);
      this.pipes = new DynamicValue(new BuiltInPipes(options.Globals));
      this.objStack.Push(new DynamicValue(data));
    }

    private string Evaluate() {
      IList<Token> t = ParseResultCache.EnsureKeyValue(str, Parse);
      for (int i = 0; i < t.Count; ) {
        switch (t[i].TokenOp) {
          case TokenOp.OP_EVAL:
            EvalToken et = (EvalToken)t[i++];
            Append(EvaluatePipe(et.Expression), et.NoEscape);
            break;
          case TokenOp.OP_ITER:
            IterationToken it = (IterationToken)t[i++];
            objStack.Push(EvaluatePipe(it.Expression));
            iteratorStack.Push(EvaluateKeys(objStack.Peek()));
            if (iteratorStack.Peek().Count == 0) {
              i = ((IterationToken)t[--i]).Index;
            }
            break;
          case TokenOp.OP_ITER_END:
            if (iteratorStack.Peek().Count <= 1) {
              objStack.Pop();
              iteratorStack.Pop();
              i++;
            } else {
              iteratorStack.Peek().Dequeue();
              i = t[i].Index;
            }
            break;
          case TokenOp.OP_TEST:
            ConditionToken ct = (ConditionToken)t[i];
            if (EvaluateCondition(ct.Conditions) ^ ct.Negate) {
              i++;
            } else {
              i = t[i].Index;
            }
            break;
          case TokenOp.OP_JUMP:
            i = t[i].Index;
            break;
          default:
            output.Append(((StringToken)t[i++]).Value);
            break;
        }
      }
      return Regex.Replace(Trim(output.ToString()), @">\s+<", "><", RegexOptions.Multiline);
    }

    private DynamicValue GetIteratorIndex() {
      if (iteratorStack.Count > 0) {
        return iteratorStack.Peek().Peek();
      }
      return DynamicValue.Null;
    }

    private DynamicValue GetCurrentObject() {
      DynamicValue obj = objStack.Peek();
      DynamicValue index = GetIteratorIndex();
      return index != DynamicValue.Null ? obj[index] : obj;
    }

    private DynamicValue Evaluate(ObjectPath objectPath) {
      if (objectPath.Count > 0 && "#".Equals(objectPath[0])) {
        return GetIteratorIndex();
      }
      DynamicValue value = GetCurrentObject();
      for (int i = 0; i < objectPath.Count; i++) {
        string key = objectPath[i];
        if (i == 0) {
          if (".".Equals(key)) {
            continue;
          }
          if ("_".Equals(key)) {
            value = objStack.ToArray()[objStack.Count - 1];
            continue;
          }
        }
        DynamicValue evaledKey = EvaluateParameter(key);
        DynamicValue evaledValue = value[evaledKey.AsString()];
        if ((!value.IsEvallable || !evaledValue.IsEvallable) && i == 0) {
          value = globals[evaledKey.AsString()];
        } else if (value.IsEvallable) {
          value = evaledValue;
        }
        if (!value.IsEvallable) {
          break;
        }
      }
      return value;
    }

    private DynamicValue EvaluateParameter(object param) {
      if (param is string) {
        string str = (string)param;
        if (str.Length > 0 && str[0] == '$') {
          return Evaluate(ParseObjectPath(str.Substring(1)));
        }
      }
      return new DynamicValue(param);
    }

    private DynamicValue EvaluatePipe(Pipe pipe, bool lazy = false) {
      DynamicValue value = Evaluate(pipe.ObjectPath);
      List<PipeArgument> args = new List<PipeArgument>(pipe.PipeArguments);
      if (!value.IsEvallable) {
        return value;
      }

      while (args.Count > 0) {
        string methodName = args[0].Value.ToString();
        args.RemoveAt(0);
        DynamicValue function = pipes[methodName];
        DynamicValue invokee = pipes;
        bool prependArg = true;
        if (function.Type != DynamicValueType.Function) {
          function = value[methodName];
          invokee = value;
          prependArg = false;
          if (function.Type != DynamicValueType.Function) {
            return DynamicValue.Null;
          }
        }

        DynamicValue[] parameters = new DynamicValue[(int)function.GetLength()];
        int i = 0;
        if (prependArg) {
          parameters[i++] = value;
        }
        for (; i < parameters.Length && args.Count > 0; i++) {
          parameters[i] = EvaluateParameter(args[0].Value);
          args.RemoveAt(0);
        }
        value = function.Invoke(invokee, parameters);
        if (lazy && !value.IsEvallable) {
          return value;
        }
      }
      return value;
    }

    private Queue<DynamicValue> EvaluateKeys(DynamicValue obj) {
      Queue<DynamicValue> queue = new Queue<DynamicValue>();
      if (obj.IsEvallable && obj.Value is DynamicNativeObject) {
        object innerObj = typeof(DynamicNativeObject).GetField("obj", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj.Value);
        if (innerObj != null) {
          if (innerObj.GetType().IsOf<IDictionary>()) {
            foreach (object v in ((IDictionary)innerObj).Keys) {
              queue.Enqueue(new DynamicValue(v));
            }
          } else if (innerObj.GetType().IsOf<IEnumerable>() && innerObj.GetType() != typeof(string)) {
            int i = 0;
            foreach (object v in (IEnumerable)innerObj) {
              queue.Enqueue(new DynamicValue(i++));
            }
          }
        }
      }
      return queue;
    }

    private bool EvaluateCondition(Pipe[] conditions) {
      foreach (Pipe v in conditions) {
        DynamicValue value = EvaluatePipe(v, true);
        if (value.IsEvallable && !false.Equals(value.Value)) {
          return true;
        }
      }
      return false;
    }

    private string Escape(string str) {
      return HttpUtility.HtmlEncode(str);
    }

    private void Append(DynamicValue value, bool noescape) {
      if (value.IsEvallable) {
        string strValue = value.AsString();
        output.Append(noescape ? strValue : Escape(strValue));
      }
    }

    private MethodInfo TryMatchMethod(DynamicValue source, string methodName, int maxArgumentCount, out object invokee) {
      MethodInfo[] methods = null;
      DynamicValue value = pipes[methodName];
      methods = (value.Value as MethodInfo[]);
      invokee = pipes.Value;
      if (methods == null) {
        if (source.IsEvallable) {
          value = source[methodName];
          methods = (value.Value as MethodInfo[]);
          invokee = source.Value;
        }
      }
      if (methods != null) {
        foreach (MethodInfo m in methods.OrderByDescending(v => v.GetParameters().Length)) {
          if (IsMethodCallable(m, maxArgumentCount)) {
            return m;
          }
        }
      }
      invokee = null;
      return null;
    }

    private bool IsMethodCallable(MethodInfo method, int maxArgumentCount) {
      return method.GetParameters().Length <= maxArgumentCount && method.GetParameters().All(v => v.ParameterType == typeof(DynamicValue) || Type.GetTypeCode(v.ParameterType) != TypeCode.Object);
    }

    internal static string Trim(string str, string chars = null, string pos = null, string replace = null) {
      if (!new DynamicValue(chars) && !new DynamicValue(pos)) {
        return Regex.Replace(str, @"^\s+|\s+$", replace ?? "");
      }
      chars = "[" + Regex.Replace(chars ?? "\\s", @"(?!\\)\]", "\\]") + "]+";
      return Regex.Replace(str, (pos ?? "^0|0$").Replace("0", chars), replace ?? "");
    }
    #endregion

    #region Parser
    private const string TOKEN_IF = "if";
    private const string TOKEN_IFNOT = "if not";
    private const string TOKEN_ELSE = "else";
    private const string TOKEN_FOREACH = "foreach";

    private static readonly Hashtable Constants = new Hashtable {
      { "true", true },
      { "false", false },
      { "null", null },
      { "0", 0 }
    };

    [DebuggerDisplay("{Value}")]
    private class PipeArgument {
      public object Value;
    }

    private class ObjectPath : List<string> {
      public ObjectPath(IEnumerable<string> s) : base(s) { }
    }

    [DebuggerDisplay("{InputString}")]
    private class Pipe {
      public string InputString;
      public ObjectPath ObjectPath;
      public List<PipeArgument> PipeArguments = new List<PipeArgument>();
    }

    private enum TokenOp {
      OP_EVAL = 1,
      OP_TEST = 2,
      OP_ITER_END = 3,
      OP_ITER = 4,
      OP_JUMP = 5
    }

    [DebuggerDisplay("{{{TokenOp}, {Index}}}")]
    private class Token {
      public TokenOp TokenOp;
      public int Index;
    }

    private class EvalToken : Token {
      public Pipe Expression;
      public bool NoEscape;
    }

    private class IterationToken : Token {
      public Pipe Expression;
    }

    private class StringToken : Token {
      public string Value;
    }

    private class ConditionToken : Token {
      public Pipe[] Conditions;
      public bool Negate;
    }

    private class ControlToken : Token {
      public int TokenIndex;
    }

    private static PipeArgument ParsePipeArgument(string str) {
      if (Constants.ContainsKey(str)) {
        return new PipeArgument { Value = Constants[str] };
      }
      if (Regex.IsMatch(str, @"^[\-+]?[0-9]*\.?[0-9]+([eE][\-+]?[0-9]+)?$")) {
        return new PipeArgument { Value = Double.Parse(str) };
      }
      return new PipeArgument { Value = str };
    }

    private static ObjectPath ParseObjectPath(string str) {
      string objectPath = Regex.Replace(str, @"^\.+|((?!\$)\.)\.+|\.+$", "$1");
      if (new DynamicValue(objectPath)) {
        return new ObjectPath(Regex.Split(objectPath, @"(?!\$)\."));
      }
      return new ObjectPath(new string[0]);
    }

    private static Pipe ParsePipe(string str) {
      Pipe pipe = new Pipe { InputString = str };
      string[] segment = Regex.Split(str, @"(?!\\)\s+");
      pipe.ObjectPath = ParseObjectPath(segment[0]);
      for (int i = 1; i < segment.Length; i++) {
        pipe.PipeArguments.Add(ParsePipeArgument(segment[i]));
      }
      return pipe;
    }

    private static Pipe[] ParseCondition(string str) {
      if (!Regex.IsMatch(str, @"(?!\\)\(")) {
        return new[] { ParsePipe(str) };
      }
      List<Pipe> t = new List<Pipe>();
      Regex r = new Regex(@"\(((?:[^)]|\\\))+)\)", RegexOptions.IgnoreCase | RegexOptions.Multiline);
      for (Match m = r.Match(str); m.Success; m = m.NextMatch()) {
        t.Add(ParsePipe(m.Groups[1].Value));
      }
      return t.ToArray();
    }

    private static IList<Token> Parse(string str) {
      List<Token> tokens = new List<Token>();
      Stack<ControlToken> controlStack = new Stack<ControlToken>();
      Regex r = new Regex(@"\{\{([\/&!:]|foreach|if(?:\s+not)?(?=\s)|else)?\s*((?:\}(?!\})|[^}])*)\}\}", RegexOptions.IgnoreCase | RegexOptions.Multiline);
      int lastIndex = 0;

      for (Match m = r.Match(str); m.Success; m = m.NextMatch()) {
        if (lastIndex != m.Index) {
          tokens.Add(new StringToken { Value = str.Substring(lastIndex, m.Index - lastIndex) });
        }
        lastIndex = m.Index + m.Value.Length;
        switch (m.Groups[1].Value) {
          case "!":
            break;
          case "/":
            Assert(str, m, controlStack.Count > 0 && ((m.Groups[2].Value == TOKEN_IF && controlStack.Peek().TokenOp == TokenOp.OP_TEST) || (m.Groups[2].Value == TOKEN_FOREACH && controlStack.Peek().TokenOp == TokenOp.OP_ITER)));
            if (m.Groups[2].Value == TOKEN_FOREACH) {
              tokens[controlStack.Peek().TokenIndex - 1].Index = tokens.Count;
              tokens.Add(new Token {
                TokenOp = TokenOp.OP_ITER_END,
                Index = controlStack.Peek().TokenIndex
              });
            } else {
              tokens[controlStack.Peek().TokenIndex].Index = tokens.Count;
            }
            controlStack.Pop();
            break;
          case TOKEN_IF:
          case TOKEN_IFNOT:
            controlStack.Push(new ControlToken {
              TokenIndex = tokens.Count,
              TokenOp = TokenOp.OP_TEST
            });
            tokens.Add(new ConditionToken {
              TokenOp = TokenOp.OP_TEST,
              Conditions = ParseCondition(m.Groups[2].Value),
              Negate = m.Groups[1].Value == TOKEN_IFNOT
            });
            break;
          case TOKEN_ELSE:
            Assert(str, m, controlStack.Peek().TokenOp == TokenOp.OP_TEST);
            ControlToken previousControl = controlStack.Pop();
            controlStack.Push(new ControlToken {
              TokenIndex = tokens.Count,
              TokenOp = TokenOp.OP_TEST
            });
            tokens[previousControl.TokenIndex].Index = tokens.Count + 1;
            tokens.Add(new Token {
              TokenOp = TokenOp.OP_JUMP
            });
            break;
          case TOKEN_FOREACH:
            tokens.Add(new IterationToken {
              TokenOp = TokenOp.OP_ITER,
              Expression = ParsePipe(m.Groups[2].Value)
            });
            controlStack.Push(new ControlToken {
              TokenIndex = tokens.Count,
              TokenOp = TokenOp.OP_ITER
            });
            break;
          case ":":
            tokens.Add(new EvalToken {
              TokenOp = TokenOp.OP_EVAL,
              Expression = ParsePipe(": " + m.Groups[2].Value),
              NoEscape = true
            });
            break;
          default:
            tokens.Add(new EvalToken {
              TokenOp = TokenOp.OP_EVAL,
              Expression = ParsePipe(new DynamicValue(m.Groups[2].Value) ? m.Groups[2].Value : "#"),
              NoEscape = m.Groups[1].Value == "&"
            });
            break;
        }
      }
      tokens.Add(new StringToken { Value = str.Substring(lastIndex) });
      return tokens;
    }

    private static void Assert(string str, Match m, bool result) {
      if (!result) {
        int start = Math.Max(0, m.Index - 10);
        int len = Math.Min(str.Length - start, m.Value.Length + 20);
        throw new TemplateParseException("Unexpected " + m.Value + " near " + str.Substring(start, len));
      }
    }
    #endregion

    #region Bulti-In Pipes
    private class BuiltInPipes : ICustomDynamicObject {
      private readonly Hashtable globals;

      public BuiltInPipes(Hashtable globals) {
        this.globals = globals;
      }

      public DynamicValue _as(DynamicValue obj, DynamicValue name) {
        globals[name.AsString()] = obj;
        return "";
      }
      public DynamicValue type(DynamicValue a) {
        switch (a.Type) {
          case DynamicValueType.Boolean:
            return "boolean";
          case DynamicValueType.Number:
            return "number";
          case DynamicValueType.String:
            return "string";
          case DynamicValueType.Object:
            return "object";
          default:
            return "undefined";
        }
      }
      public DynamicValue date(DynamicValue value) {
        if (value.Value is DateTime) {
          return value;
        }
        if (value.Value is Double) {
          return new DynamicValue(DateTimeExtension.FromJavaScriptTimestamp((long)value.Value, DateTimeKind.Local));
        }
        return new DynamicValue(DateTime.Parse(new DynamicValue(value).AsString()));
      }
      public DynamicValue _bool(DynamicValue obj) {
        return obj.AsBool();
      }
      public DynamicValue _true(DynamicValue a) {
        return true.Equals(a.Value);
      }
      public DynamicValue _false(DynamicValue a) {
        return false.Equals(a.Value);
      }
      public DynamicValue not(DynamicValue obj) {
        return !obj;
      }
      public DynamicValue or(DynamicValue obj, DynamicValue val) {
        return obj || val;
      }
      public DynamicValue empty(DynamicValue obj) {
        return !obj || obj.GetLength() == 0 ? obj : new DynamicValue(false);
      }
      public DynamicValue notempty(DynamicValue obj) {
        return obj && obj.GetLength() > 0 ? obj : new DynamicValue(false);
      }
      public DynamicValue more(DynamicValue a, DynamicValue b) {
        return a.GetLength() > b ? a : new DynamicValue(false);
      }
      public DynamicValue less(DynamicValue a, DynamicValue b) {
        return a.GetLength() < b ? a : new DynamicValue(false);
      }
      public DynamicValue ormore(DynamicValue a, DynamicValue b) {
        return a.GetLength() >= b ? a : new DynamicValue(false);
      }
      public DynamicValue orless(DynamicValue a, DynamicValue b) {
        return a.GetLength() <= b ? a : new DynamicValue(false);
      }
      public DynamicValue between(DynamicValue a, DynamicValue b, DynamicValue c) {
        double doubleValue = a.GetLength();
        return doubleValue >= b && doubleValue <= c ? a : new DynamicValue(false);
      }
      public DynamicValue equals(DynamicValue a, DynamicValue b) {
        return a.AsString() == b.AsString() ? a : new DynamicValue(false);
      }
      public DynamicValue notequals(DynamicValue a, DynamicValue b) {
        return a.AsString() != b.AsString() ? a : new DynamicValue(false);
      }
      public DynamicValue even(DynamicValue num) {
        return ((long)num.AsNumber() & 1) == 0 ? num : new DynamicValue(false);
      }
      public DynamicValue odd(DynamicValue num) {
        return ((long)num.AsNumber() & 1) == 1 ? num : new DynamicValue(false);
      }
      public DynamicValue choose(DynamicValue a, DynamicValue trueValue, DynamicValue falseValue) {
        return a ? trueValue : falseValue;
      }
      public DynamicValue concat(DynamicValue str, DynamicValue str2) {
        return str.AsString() + str2.AsString();
      }
      public DynamicValue trim(DynamicValue str, DynamicValue chars) {
        return Template.Trim(str, chars);
      }
      public DynamicValue trimstart(DynamicValue str, DynamicValue chars) {
        return Template.Trim(str, chars, "^0");
      }
      public DynamicValue trimend(DynamicValue str, DynamicValue chars) {
        return Template.Trim(str, chars, "0$");
      }
      public DynamicValue padstart(DynamicValue str, DynamicValue chars) {
        return Template.Trim(str, chars, "^(?!0)", chars);
      }
      public DynamicValue padend(DynamicValue str, DynamicValue chars) {
        return Template.Trim(str, chars, "(?!0)$", chars);
      }
      public DynamicValue split(DynamicValue str, DynamicValue separator) {
        return str.AsString().Split(new[] { separator.AsString() }, StringSplitOptions.None);
      }
      public DynamicValue length(DynamicValue obj) {
        return obj.GetLength();
      }
      public DynamicValue sort(DynamicValue arr, DynamicValue prop) {
        IEnumerable<DynamicValue> typedArr = arr.AsArray();
        return new DynamicValue(typedArr.OrderBy(v => v[prop]));
      }
      public DynamicValue reverse(DynamicValue arr) {
        IEnumerable<DynamicValue> typedArr = arr.AsArray();
        return new DynamicValue(typedArr.Reverse());
      }
      public DynamicValue plus(DynamicValue a, DynamicValue b) {
        return a.AsNumber() + b.AsNumber();
      }
      public DynamicValue minus(DynamicValue a, DynamicValue b) {
        return a - b;
      }
      public DynamicValue multiply(DynamicValue a, DynamicValue b) {
        return a * b;
      }
      public DynamicValue divide(DynamicValue a, DynamicValue b) {
        return a / b;
      }
      public DynamicValue mod(DynamicValue a, DynamicValue n) {
        return a % n;
      }

      #region ICustomDynamicTypeObject
      string ICustomDynamicObject.TypeName {
        get { return "BultiInPipes"; }
      }

      IEnumerable<DynamicKey> ICustomDynamicObject.GetKeys() {
        List<DynamicKey> keys = new List<DynamicKey>();
        foreach (MethodInfo method in typeof(BuiltInPipes).GetMethods()) {
          switch (method.Name) {
            case "_as":
              keys.Add(new DynamicKey("as"));
              break;
            case "_bool":
              keys.Add(new DynamicKey("bool"));
              break;
            case "_true":
              keys.Add(new DynamicKey("true!"));
              break;
            case "_false":
              keys.Add(new DynamicKey("false!"));
              break;
            default:
              keys.Add(new DynamicKey(method.Name));
              break;
          }
        }
        return keys.ToArray();
      }

      bool ICustomDynamicObject.GetValue(string key, out object value) {
        value = null;
        switch (key) {
          case "as":
            value = new MethodInfo[] { ((Func<DynamicValue, DynamicValue, DynamicValue>)_as).Method };
            break;
          case "bool":
            value = new MethodInfo[] { ((Func<DynamicValue, DynamicValue>)_bool).Method };
            break;
          case "true!":
            value = new MethodInfo[] { ((Func<DynamicValue, DynamicValue>)_true).Method };
            break;
          case "false!":
            value = new MethodInfo[] { ((Func<DynamicValue, DynamicValue>)_false).Method };
            break;
          case "ToString":
          case "GetHashCode":
          case "Equals":
            return false;
          default:
            MethodInfo method = typeof(BuiltInPipes).GetMethod(key);
            if (method != null) {
              value = new MethodInfo[] { method };
              return true;
            }
            break;
        }
        return (value != null);
      }

      void ICustomDynamicObject.SetValue(string key, object value) {
        throw new NotImplementedException();
      }

      void ICustomDynamicObject.DeleteKey(string key) {
        throw new NotImplementedException();
      }
      #endregion
    }
    #endregion
  }
}

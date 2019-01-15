using IQToolkit;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace Codeless.SharePoint.Internal {
  [DebuggerDisplay("{Expression,nq}")]
  internal class ParameterizedExpression : IEquatable<ParameterizedExpression> {
    private readonly LambdaExpression expression;
    private readonly int hashCode;

    private ParameterizedExpression(LambdaExpression expression, int hashCode) {
      this.expression = expression;
      this.hashCode = hashCode;
    }

    public Expression Expression {
      get { return expression; }
    }

    public ReadOnlyCollection<ParameterExpression> Parameters {
      get { return expression.Parameters; }
    }

    public static ParameterizedExpression Create(Expression expression, out object[] args) {
      CommonHelper.ConfirmNotNull(expression, "expression");
      return new ExpressionParameterizeVisitor().Visit(expression, out args);
    }

    public bool Equals(ParameterizedExpression other) {
      if (other == null || hashCode != other.hashCode) {
        return false;
      }
      return ExpressionComparer.AreEqual(expression, other.expression);
    }

    public override bool Equals(object obj) {
      ParameterizedExpression other = obj as ParameterizedExpression;
      return other != null && Equals(other);
    }

    public override int GetHashCode() {
      return hashCode;
    }

    private class ExpressionParameterizeVisitor : IQToolkit.ExpressionVisitor {
      private static readonly ModuleBuilder module;
      private static readonly Type[] builtInFuncTypes;
      private static readonly ConcurrentFactory<int, Type> funcTypes = new ConcurrentFactory<int, Type>();
      private readonly List<object> arguments = new List<object>();
      private readonly List<ParameterExpression> parameters = new List<ParameterExpression>();
      private int hashCode = 5381;

      static ExpressionParameterizeVisitor() {
        Assembly assembly = typeof(Func<>).Assembly;
        List<Type> list = new List<Type>();
        for (int i = 1; ; i++) {
          Type funcType = assembly.GetType("System.Func`" + i);
          if (funcType == null) {
            break;
          }
          list.Add(funcType);
        }
        builtInFuncTypes = list.ToArray();

        AssemblyBuilder builder = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName("DelegateTypeFactory"), AssemblyBuilderAccess.Run);
        module = builder.DefineDynamicModule("DelegateTypeFactory");
      }

      public ParameterizedExpression Visit(Expression expression, out object[] args) {
        Expression body = Visit(expression);
        Type genericType = parameters.Count < builtInFuncTypes.Length ? builtInFuncTypes[parameters.Count] : funcTypes.EnsureKeyValue(parameters.Count, CreateGenericFuncType);
        Type funcType = genericType.MakeGenericType(parameters.Select(v => v.Type).Concat(new[] { body.Type }).ToArray());
        args = arguments.ToArray();
        return new ParameterizedExpression(Expression.Lambda(funcType, body, parameters.ToArray()), hashCode);
      }

      protected override Expression Visit(Expression expression) {
        if (expression != null) {
          hashCode = ((hashCode << 5) + hashCode) ^ expression.NodeType.GetHashCode();
          hashCode = ((hashCode << 5) + hashCode) ^ expression.Type.GetHashCode();
        }
        return base.Visit(expression);
      }

      protected override Expression VisitConstant(ConstantExpression expression) {
        ParameterExpression param = Expression.Parameter(expression.Type, "p" + arguments.Count);
        parameters.Add(param);
        arguments.Add(expression.Value);
        return param;
      }

      private static Type CreateGenericFuncType(int paramCount) {
        string[] typeArgNames = new string[paramCount + 1];
        for (int i = 0; i < paramCount; i++) {
          typeArgNames[i] = "T" + i;
        }
        typeArgNames[paramCount] = "TResult";

        TypeBuilder typeBuilder = module.DefineType("Func`" + paramCount, TypeAttributes.Sealed | TypeAttributes.Public, typeof(MulticastDelegate));
        GenericTypeParameterBuilder[] genericParameters = typeBuilder.DefineGenericParameters(typeArgNames);

        ConstructorBuilder constructor = typeBuilder.DefineConstructor(MethodAttributes.RTSpecialName | MethodAttributes.HideBySig | MethodAttributes.Public, CallingConventions.Standard, new[] { typeof(object), typeof(IntPtr) });
        constructor.SetImplementationFlags(MethodImplAttributes.CodeTypeMask);

        MethodBuilder invokeMethod = typeBuilder.DefineMethod("Invoke", MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.Public, genericParameters[paramCount], genericParameters.Take(paramCount).ToArray());
        invokeMethod.SetImplementationFlags(MethodImplAttributes.CodeTypeMask);
        for (int i = 0; i < paramCount; i++) {
          invokeMethod.DefineParameter(i + 1, ParameterAttributes.None, genericParameters[i].Name);
        }
        return typeBuilder.CreateType();
      }
    }
  }
}


using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace ChainableSearch.Services
{
    public static class ExpressionHelper
    {
        private static readonly Dictionary<string, Func<object, object>> Funcs = new Dictionary<string, Func<object, object>>();
        private static readonly Dictionary<string, Action<object>> Actions = new Dictionary<string, Action<object>>();

        /// <summary>
        /// Say there is a method with no arguments but a type parameter T. You want to call the method, but you only
        /// have the type in a variable. This will generate a call of the method using that type as a type parameter.
        /// </summary>
        public static object InvokeGenericFuncWithKnownType(string cacheAppendix, object state, Expression<Func<object, object>> sample, params Type[] types)
        {
            // in the comments, we call the sample method InternalSearch
            var originalCall = sample.Body as MethodCallExpression; // InternalSearch<object>(searchId, matcher, getter);

            var originalMethod = originalCall.Method;   // InternalSearch<object>
            var openMethod = originalMethod.GetGenericMethodDefinition(); // InternalSearch<>
            var closedMethod = openMethod.MakeGenericMethod(types); // InternalSearch<T>

            // InternalSearch<T>(searchId, matcher, getter);
            var closedBody = Expression.Call(originalCall.Object, closedMethod, originalCall.Arguments);

            // () => InternalSearch<T>(searchId, matcher, getter);
            var lambda = Expression.Lambda<Func<object, object>>(closedBody, sample.Parameters);

            Func<object, object> func;

            // cache key as lambda.Compile is slow
            var typeNames = String.Join(".", types.Select(z => z.Name)) + originalMethod.Name + cacheAppendix;
            if (Funcs.ContainsKey(typeNames))
            {
                func = Funcs[typeNames];
            }
            else
            {
                func = lambda.Compile();
                Funcs.Add(typeNames, func);
            }

            // invoke
            return func(state);
        }

        public static void CacheableInvokeGenericActionWithKnownType(string cacheAppendix, object state, Expression<Action<object>> sample, params Type[] types)
        {
            var originalCall = sample.Body as MethodCallExpression;
            var originalMethod = originalCall.Method;
            var openMethod = originalMethod.GetGenericMethodDefinition();
            var closedMethod = openMethod.MakeGenericMethod(types);

            var closedBody = Expression.Call(originalCall.Object, closedMethod, originalCall.Arguments);

            var lambda = Expression.Lambda<Action<object>>(closedBody, sample.Parameters);

            Action<object> action;
            var typeNames = string.Join(".", types.Select(z => z.Name)) + originalMethod.Name + cacheAppendix;
            if (Actions.ContainsKey(typeNames))
            {
                action = Actions[typeNames];
            }
            else
            {
                action = lambda.Compile();
                Actions.Add(typeNames, action);
            }

            // invoke
            action(state);
        }

        /// <summary>
        /// Creates a property setter action given a getter expression.
        /// </summary>
        public static Action<TEntity, TValue> CreatePropertySetter<TEntity, TValue>(Expression<Func<TEntity, TValue>> getter)
        {
            var expr = getter.Body as MemberExpression;

            // intercept type conversion. This will occur for Enum --> int or nullable integer
            Type innerValueType = null;
            if (getter.Body.NodeType == ExpressionType.Convert)
            {
                var conversion = getter.Body as UnaryExpression;
                expr = conversion.Operand as MemberExpression;
                innerValueType = expr.Type;
            }

            var property = expr.Member as PropertyInfo;

            // Parameters. The first parameter is the object being modified. The second parameter is the value going into the property
            var param = Expression.Parameter(typeof(TEntity), "i");
            var value = Expression.Parameter(typeof(TValue), "j");

            // This is the normal case
            Expression input = value;

            if (innerValueType != null)
            {
                // (MyEnumType)j
                input = Expression.Convert(input, innerValueType);
            }

            // i.FirstName
            var member = Expression.Property(param, property);

            // i.FirstName = j. Or, "i.FirstName = (MyEnumType)j".
            var assignment = Expression.Assign(member, input);

            // (i, j) => i.FirstName = j
            var lambda = Expression.Lambda<Action<TEntity, TValue>>(assignment, new[] { param, value });
            var result = lambda.Compile();

            return result;
        }

        public static Expression<Func<TEntity, bool>> BuildEntityKeyExpression<TKey, TEntity>(TKey keyValue, Expression<Func<TEntity, TKey>> keyExpression) where TEntity : class
        {
            var memberExpression = keyExpression.Body;
            if (memberExpression is UnaryExpression unaryExpression)
            {
                memberExpression = unaryExpression.Operand;
            }

            var keyType = typeof(TKey);
            var binaryExpression = Nullable.GetUnderlyingType(keyType) == null
                ? Expression.Equal(memberExpression, Expression.Constant(keyValue))
                : Expression.Equal(memberExpression, Expression.Convert(Expression.Constant(keyValue), keyType));

            var expression = Expression.Lambda<Func<TEntity, bool>>(binaryExpression, keyExpression.Parameters);

            return expression;
        }
    }
}

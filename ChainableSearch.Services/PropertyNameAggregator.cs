using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace ChainableSearch.Services
{
    public class PropertyNameAggregator
    {
        public PropertyMetaData PropertyMetaData { get; set; }

        public PropertyNameAggregator()
        {
            PropertyMetaData = new PropertyMetaData
            {
                PropertyInfoList = new List<PropertyInfo>()
            };
        }

        #region Visit Expression Tree (restricted)

        /// <summary>
        /// We support a very limit set of expressions:
        /// 1) Unary (convert int? to int, maybe)
        /// 2) PropertyExpression
        /// 3) Enumerable.Select(expr, lambda). 
        /// </summary>
        public void Visit(Expression expr, bool reEntrant)
        {
            if (!reEntrant)
            {
                PropertyMetaData = new PropertyMetaData
                {
                    PropertyInfoList = new List<PropertyInfo>()
                };
            }

            // 0) root. This is when we're back to the "z" in "z => z.[...]"
            if (expr is ParameterExpression)
            {
                PropertyMetaData.Type = expr.Type;
                return;
            }

            // 1) Unary (convert int? to int, maybe)
            if (expr is UnaryExpression unary)
            {
                VisitUnary(unary);
                return;
            }

            // 2) PropertyExpression
            if (expr is MemberExpression prop)
            {
                VisitProperty(prop);
                return;
            }

            // 3) Enumerable.Select(expr, lambda) (we hope)
            if (expr is MethodCallExpression call)
            {
                VisitCall(call);
                return;
            }

            if (expr is BinaryExpression binary)
            {
                VisitBinary(binary);
                return;
            }

            if (expr is ConstantExpression)
            {
                return;
            }

            // everything else is not supported
            Error("{0} expressions are not supported", expr.NodeType);
        }

        private void VisitUnary(UnaryExpression unary)
        {
            if (unary.NodeType == ExpressionType.Convert)
            {
                // allow and continue
                Visit(unary.Operand, true);
            }
            else
            {
                Error("Unary expressions of node type '{0}' are not supported.", unary.NodeType);
            }
        }

        private void VisitProperty(MemberExpression expr)
        {
            var propertyInfo = expr.Member as PropertyInfo;
            if (propertyInfo == null)
            {
                Error("Only properties are supported, not fields.");
            }

            PropertyMetaData.PropertyInfoList.Add(propertyInfo);

            // keep going
            Visit(expr.Expression, true);
        }

        private void VisitCall(MethodCallExpression expr)
        {
            // handle toString()
            if (expr.Method.Name == "ToString")
            {
                Visit(expr.Object, true);

                return;
            }

            // only Enumerable.Select is supported
            var method = expr.Method;
            if ((method.DeclaringType != typeof(Enumerable)) || (method.Name != "Select"))
            {
                Error("The only supported method call is the extension method IEnumerable<T>.Select()");
            }

            var selectorArgument = expr.Arguments[1];
            var lambda = selectorArgument as LambdaExpression;
            if (lambda == null)
            {
                Error("The only supported argument to IEnumerable<T>.Select() is a lambda expression.");
            }

            // visit the right hand side
            Visit(lambda.Body, true);

            // visit the parent part (arg 0 to Enumerable.Select is the "this" part of the extension method)
            Visit(expr.Arguments[0], true);
        }

        private void VisitBinary(BinaryExpression expr)
        {
            if (expr.Left is BinaryExpression left)
            {
                VisitBinary(left);
            }

            Visit(expr.Left, true);
            Visit(expr.Right, true);
        }

        #endregion

        /// <summary>
        /// Helper method to throw a well formatted exception.
        /// </summary>
        private static void Error(string format, params object[] args)
        {
            var message = string.Format(format, args);
            throw new Exception(message);
        }
    }
}

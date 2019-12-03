using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using static System.Linq.Expressions.Expression;

namespace HotChocolate.Types.Projections {
    internal static class QueryableExtensions {

        private static MethodCallExpression CallSimpleExtensionMethod(this Expression exp, Type extensionType, string methodName, Type[] typeArguments, Expression arg) =>
            Call(extensionType, methodName, typeArguments, exp, arg)
        ;
        private static Expression CallSimpleExtensionMethodIf(this Expression exp, bool condition, Type extensionType, string methodName, Type[] typeArguments, Expression arg) =>
            condition
            ? Call(extensionType, methodName, typeArguments, exp, arg)
            : exp
        ;

        private static MemberExpression Property(this Expression exp, string propertyName) => PropertyOrField(exp, propertyName);
        private static Expression<TDelegate> SimpleLambda<TDelegate>(this ParameterExpression param, Func<ParameterExpression, Expression> getBody) =>
            Lambda<TDelegate>(getBody(param), param)
        ;
        private static LambdaExpression SimpleUntypedLambda(this ParameterExpression param, Func<ParameterExpression, Expression> getBody) =>
            Lambda(getBody(param), param)
        ;
        private static MemberAssignment Assign(this MemberInfo member, Expression expression) => Bind(member, expression);
        private static MemberInitExpression Initialize(this NewExpression newItem, IEnumerable<MemberBinding> bindings) => MemberInit(newItem, bindings);

        // member = param.member
        private static MemberBinding BindScalarMember(this ParameterExpression param, MemberInfo member) => member.Assign(param.Property(member.Name));

        // member =
        //     param.member
        //     .Where<memberType>(filterExpression)
        //     .Select<memberType, memberType>((memberType p) => 
        //         new memberType {
        //             prop1 = p.prop1,
        //             ...
        //         }
        //     )
        // ,
        private static MemberBinding BindListMember(this ParameterExpression param, MemberInfo member, Type memberType, IEnumerable<PropertyType> props, Expression? filterExpression) =>
            member.Assign(
                param.Property(member.Name)
                .CallSimpleExtensionMethodIf(filterExpression is { }, typeof(Enumerable), nameof(Enumerable.Where), new[] { memberType }, filterExpression!)
                .CallSimpleExtensionMethod(typeof(Enumerable), nameof(Enumerable.Select), new[] { memberType, memberType }, Parameter(memberType).SimpleUntypedLambda(p =>
                    New(memberType).Initialize(
                        props.Select(p.BindMember)
                    )
                ))
            )
        ;

        private static MemberBinding BindMember(this ParameterExpression param, PropertyType property) =>
            // Determine strategy for selecting properties in the final projection.
            // For scalar types, the value can simply be copied.
            // For complex list types, perform a projection on the list choosing which properties are projected on the inner type
            property.Either(param.BindScalarMember, param.BindListMember)
        ;

        /// <summary>
        /// Projects the provided properties for each item in the collection.
        /// </summary>
        /// <param name="props">The list of properties to project.</param>
        public static IQueryable<TResult> Select<TResult>(this IQueryable<TResult> source, IEnumerable<PropertyType> props) =>
            // source.Select((TResult item) => new TResult {
            //     prop1 = item.prop1,
            //     ...
            // })
            source.Select(Parameter(typeof(TResult)).SimpleLambda<Func<TResult, TResult>>(item => New(typeof(TResult)).Initialize(
                props.Select(item.BindMember)
            )))
        ;

    }
}

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace HotChocolate.Types.Projections {
    /// <summary>
    /// A union to represent various types of properties.  Currently only scalar types and complex list types are implemented.
    /// </summary>
    internal abstract class PropertyType {
        private PropertyType() { }
        private class Scalar : PropertyType {
            public MemberInfo Member { get; }
            public Scalar(MemberInfo member) => Member = member;
        }
        private class List : PropertyType {
            public MemberInfo Member { get; }
            public Type MemberType { get; }
            public IEnumerable<PropertyType> Properties { get; }
            public Expression? FilterExpression { get; }
            public List(MemberInfo member, Type memberType, IEnumerable<PropertyType> properties, Expression? filterExpression) {
                Member = member;
                MemberType = memberType;
                Properties = properties;
                FilterExpression = filterExpression;
            }
        }
        public static PropertyType CreateScalar(MemberInfo member) => new Scalar(member);
        public static PropertyType CreateList(MemberInfo member, Type memberType, IEnumerable<PropertyType> properties, Expression? filterExpression) =>
            new List(member, memberType, properties, filterExpression)
        ;

        public T Either<T>(Func<MemberInfo, T> scalarSelector, Func<MemberInfo, Type, IEnumerable<PropertyType>, Expression?, T> listSelector) =>
            this switch
            {
                Scalar s => scalarSelector(s.Member),
                List l => listSelector(l.Member, l.MemberType, l.Properties, l.FilterExpression),
                _ => throw new InvalidOperationException("Type matching search is not exhaustive")
            }
        ;
    }
}

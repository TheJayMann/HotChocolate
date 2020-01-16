using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using HotChocolate.Execution;
using HotChocolate.Language;
using HotChocolate.Resolvers;
using HotChocolate.Types.Filters;
using HotChocolate.Types.Relay;
using HotChocolate.Utilities;

namespace HotChocolate.Types.Projections {
    using IHasName = HotChocolate.Language.IHasName;

    internal class QueryableProjectionMiddleware<T> {
        private readonly FieldDelegate _Next;
        private readonly ITypeConversion _Converter;

        public QueryableProjectionMiddleware(FieldDelegate next, ITypeConversion converter) {
            _Next = next ?? throw new ArgumentNullException(nameof(next));
            _Converter = converter ?? TypeConversion.Default;
        }
        private IEnumerable<PropertyType>? GetPropertiesFromConnection(IMiddlewareContext ctx, IConnectionType connectionType) {
            if (connectionType is { EdgeType: { EntityType: ObjectType<T> t } }) {
                // For paging using a connection type, currently the nodes field is used to determine the projection used.  However, it would also be valid to use
                // edges/node instead.  Having both specified could cause issues, especially if they specify different projections.
                if (ctx.FieldSelection.SelectionSet?.Selections?.SingleOrDefault(s => s is IHasName { Name: { Value: "nodes" } }) is FieldNode { SelectionSet: { } selectionSet }) {
                    return GetProperties(ctx, t, selectionSet);
                }
            }
            return null;
        }
        private IEnumerable<PropertyType>? GetPropertiesFromObject(IMiddlewareContext ctx, ObjectType<T> objectType) =>
            ctx.FieldSelection.SelectionSet is { } selectionSet
            ? GetProperties(ctx, objectType, selectionSet)
            : null
        ;

        public async Task InvokeAsync(IMiddlewareContext ctx) {

            await _Next(ctx).ConfigureAwait(false);

            var props = GetInnermostType(ctx.Field.Type) switch
            {
                IConnectionType t => GetPropertiesFromConnection(ctx, t), // Getting the object type from connection type when using paging
                ObjectType<T> t => GetPropertiesFromObject(ctx, t), // Getting the object type directly
                _ => null, // All other cases, don't perform projections as projections are unnecessary
            };

            if (props is null) return; // If unable to get the object type, don't perform any projections

            // Apply the projection operation on the result based on the list of properties specified in the query
            ctx.Result = ctx.Result switch
            {
                PageableData<T> p => new PageableData<T>(p.Source.Select(props)),
                IQueryable<T> q => q.Select(props),
                var r => r,
            };
        }

        private static IType GetInnermostType(IType type) => type.InnerType() switch
        {
            ListType innerType => GetInnermostType(innerType),
            NonNullType innerType => GetInnermostType(innerType),
            var innerType => innerType,
        };

        /// <summary>
        /// Recursively collects all fields of an object type creating a sequence of property types.
        /// </summary>
        private IEnumerable<PropertyType> GetProperties(IResolverContext ctx, ObjectType type, SelectionSetNode selectionSet) {
            foreach (var field in ctx.CollectFields(type, selectionSet)) {
                object fieldType = field.Field.Type;
retrySwitch:
                switch (fieldType) {
                    case NonNullType n: {
                        fieldType = n.Type;
                        goto retrySwitch;
                    }
                    case ScalarType _: {
                        yield return PropertyType.CreateScalar(field.Field.Member);
                        break;
                    }
                    case ListType l when l.InnerType() is ObjectType t: {
                        // When we have a list of a complex type, we check to see if any filters exist in the query and collect the nested fields
                        if (field.Selection.SelectionSet is { } fieldSelectionSet) {
                            Expression? filterExpression = null;
                            if (field is FieldSelection f) {
                                var args = f.CoerceArguments(null, _Converter);
                                if (args.TryGetValue("where", out var filterArg) && filterArg.Literal is IValueNode filter && !(filter is NullValueNode)) {
                                    if (field.Field.Arguments["where"].Type is InputObjectType iot && iot is IFilterInputType { EntityType: var fet }) {
                                        var visitor = new QueryableProjectionFilterVisitor(iot, fet, _Converter);
                                        filter.Accept(visitor);
                                        filterExpression = visitor.CreateFilter();
                                    }
                                }

                            }

                            yield return PropertyType.CreateList(field.Field.Member, t.ClrType, GetProperties(ctx, t, fieldSelectionSet), filterExpression);
                        }
                        break;
                    }
                }
            }
        }

    }
}

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using HotChocolate.Types.Filters;
using HotChocolate.Types.Filters.Expressions;
using HotChocolate.Utilities;

namespace HotChocolate.Types.Projections {
    // The purpose for this class is to gain access to the protected Closures property, in order to create a LambdaExpression without having to specify
    // types at compile time.
    internal class QueryableProjectionFilterVisitor : QueryableFilterVisitor {
        public QueryableProjectionFilterVisitor(InputObjectType initialType, Type source, ITypeConversion converter) : base(initialType, source, converter) { }


        public QueryableProjectionFilterVisitor(InputObjectType initialType, Type source, ITypeConversion converter, IEnumerable<IExpressionOperationHandler> operationHandlers, IEnumerable<IExpressionFieldHandler> fieldHandlers) : base(initialType, source, converter, operationHandlers, fieldHandlers) { }

        public Expression CreateFilter() =>
            Closures.Peek().CreateLambda()
        ;
    }
}

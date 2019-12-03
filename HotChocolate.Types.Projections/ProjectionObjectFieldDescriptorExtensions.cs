using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using HotChocolate.Types.Projections;

namespace HotChocolate.Types {
    public static class ProjectionObjectFieldDescriptorExtensions {
        private static IObjectFieldDescriptor UseEntityProjection<T, TResult>(IObjectFieldDescriptor field, Func<IAsyncEnumerable<T>, Task<TResult>> asyncEntity, Func<IQueryable<T>, TResult> queryEntity, Func<IEnumerable<T>, TResult> selectEntity) =>
            field
            .Use(next => async ctx => {
                await next(ctx).ConfigureAwait(false);
                ctx.Result = ctx.Result switch
                {
                    IAsyncEnumerable<T> a => await asyncEntity(a).ConfigureAwait(false),
                    IQueryable<T> q => queryEntity(q),
                    IEnumerable<T> e => selectEntity(e),
                    var r => r,
                };
            })
            .Use<QueryableProjectionMiddleware<T>>()
        ;
        [return: MaybeNull]
        private static async Task<T> SingleOrDefaultAsync<T>(this IAsyncEnumerable<T> source) where T : class {
            var retval = default(T)!;
            await foreach (var item in source.ConfigureAwait(false)) {
                if (retval is T) throw new InvalidOperationException("Sequence contains more than one element");
                retval = item;
            }
            return retval;
        }

        [return: MaybeNull]
        private static async Task<T> FirstOrDefaultAsync<T>(this IAsyncEnumerable<T> source) where T : class {
            await foreach (var item in source.ConfigureAwait(false)) {
                return item;
            }
            return default!;
        }

        /// <summary>
        /// Adds the projection middleware to the field while asserting the field has only one item.
        /// </summary>
        public static IObjectFieldDescriptor UseSingleProjection<T>(this IObjectFieldDescriptor field) where T : class => UseEntityProjection<T, T>(field, SingleOrDefaultAsync, Queryable.SingleOrDefault, Enumerable.SingleOrDefault);
        /// <summary>
        /// Adds the projection middleware to the field while returning the first available item.
        /// </summary>
        public static IObjectFieldDescriptor UseFirstProjection<T>(this IObjectFieldDescriptor field) where T : class => UseEntityProjection<T, T>(field, FirstOrDefaultAsync, Queryable.FirstOrDefault, Enumerable.FirstOrDefault);
        /// <summary>
        /// Adds the projection middleware to the field.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="field"></param>
        /// <returns></returns>
        public static IObjectFieldDescriptor UseListProjection<T>(this IObjectFieldDescriptor field) => field.Use<QueryableProjectionMiddleware<T>>();
    }
}

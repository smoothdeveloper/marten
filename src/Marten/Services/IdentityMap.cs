using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Baseline;

namespace Marten.Services
{
    public class IdentityMap : IIdentityMap
    {
        private readonly ISerializer _serializer;

        private readonly Cache<Type, ConcurrentDictionary<int, object>> _objects 
            = new Cache<Type, ConcurrentDictionary<int, object>>(_ => new ConcurrentDictionary<int, object>());

        public IdentityMap(ISerializer serializer)
        {
            _serializer = serializer;
        }

        public T Get<T>(object id, Func<string> json) where T : class
        {
            return _objects[typeof (T)].GetOrAdd(id.GetHashCode(), _ =>
            {
                var text = json();
                return Deserialize<T>(text);
            }).As<T>();
        }

        public async Task<T> GetAsync<T>(object id, Func<CancellationToken, Task<string>> json, CancellationToken token) where T : class
        {
            var jsonString = await json(token).ConfigureAwait(false);
            return Get<T>(id, jsonString);
        }

        public T Get<T>(object id, string json) where T : class
        {
            return _objects[typeof(T)].GetOrAdd(id.GetHashCode(), _ =>
            {
                return Deserialize<T>(json);
            }).As<T>();
        }

        public void Remove<T>(object id)
        {
            object value;
            _objects[typeof (T)].TryRemove(id.GetHashCode(), out value);
        }

        public void Store<T>(object id, T entity)
        {
            _objects[typeof (T)].AddOrUpdate(id.GetHashCode(), entity, (i, e) => e);
        }

        public bool Has<T>(object id)
        {
            var dict = _objects[typeof (T)];
            var hashCode = id.GetHashCode();
            return dict.ContainsKey(hashCode) && dict[hashCode] != null;
        }

        public T Retrieve<T>(object id) where T : class
        {
            var dict = _objects[typeof(T)];
            var hashCode = id.GetHashCode();
            return dict.ContainsKey(hashCode) ? dict[hashCode] as T : null;
        }

        private T Deserialize<T>(string text) where T : class
        {
            if (text.IsEmpty())
            {
                return null;
            }

            return _serializer.FromJson<T>(text);
        }
    }
}
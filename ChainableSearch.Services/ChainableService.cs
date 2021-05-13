using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using ChainableSearch.Model;

namespace ChainableSearch.Services
{
    public class ChainableService
    {
        public string ApiName { get; set; }
        public object SearchId { get; set; }
        public object SearchResult { get; set; }
        private readonly Dictionary<Type, Expression<Func<object, object>>> _searchers = new Dictionary<Type, Expression<Func<object, object>>>();
        private readonly Dictionary<Type, Expression<Action<object>>> _inserters = new Dictionary<Type, Expression<Action<object>>>();
        private string _sourceFieldName;
        private string _targetFieldName;
        protected ResponseInfo ResponseInfo { get; set; }

        public ChainableService()
        {
            ResponseInfo = new ResponseInfo();
        }

        /// <summary>
        /// Using matcher to search table T, return the field specified via matcher
        /// </summary>
        /// <typeparam name="T">Table to be searched</typeparam>
        /// <param name="matcher">search field expression</param>
        /// <param name="getter">return field func</param>
        /// <returns>The current class instance so that we can chain Search()</returns>
        public ChainableService Search<T>(Expression<Func<T, object>> matcher, Expression<Func<T, object>> getter) where T : class
        {
            var simplePropertyNameAggregator = new PropertyNameAggregator();
            if (string.IsNullOrEmpty(_sourceFieldName))
            {
                simplePropertyNameAggregator.Visit(matcher.Body, false);
                _sourceFieldName = simplePropertyNameAggregator.PropertyMetaData.PropertyInfoList.Last().Name;
            }

            if (string.IsNullOrEmpty(_targetFieldName))
            {
                simplePropertyNameAggregator.Visit(getter.Body, false);
                _targetFieldName = simplePropertyNameAggregator.PropertyMetaData.PropertyInfoList.Last().Name;
            }

            // define search expression so that we can invoke it later.
            Expression<Func<object, object>> searcher = z => InternalSearch(z, matcher, getter.Compile());

            if (!_searchers.ContainsKey(typeof(T)))
            {
                _searchers.Add(typeof(T), searcher);
            }

            return this;
        }

        /// <summary>
        /// Using setter to set property of table T, then return the newly generated row ID via getter
        /// </summary>
        /// <typeparam name="T">Table type to be inserted</typeparam>
        /// <param name="setter">used to set ID of newly created entity with type T, e.g., Id in Contact class</param>
        /// <param name="returner">used to get returned field of entity with type T, such as Name in Contact class</param>
        /// <returns></returns>
        public ChainableService Insert<T>(Expression<Func<T, object>> setter, Func<T, object> returner) where T : class, new()
        {
            Expression<Action<object>> inserter = z => InternalInsert(z, setter, returner);
            if (!_inserters.ContainsKey(typeof(T)))
            {
                _inserters.Add(typeof(T), inserter);
            }

            return this;
        }

        /// <summary>
        /// Invoke predefined searchers and inserters with their generic types
        /// </summary>
        /// <param name="searchId">Identifier to be searched, e.g., ID from a Customer</param>
        /// <returns></returns>
        public ResponseInfo Action(object searchId)
        {
            SearchId = searchId;
            var isNumber = int.TryParse(SearchId.ToString(), out int parsedInteger);
            if (SearchId == null || SearchId.ToString() == string.Empty || (isNumber && parsedInteger == 0))
            {
                ResponseInfo.IsSuccessful = false;
                ResponseInfo.ErrorMessage = $"{_sourceFieldName} passed as null, empty string or zero";
                return ResponseInfo;
            }

            foreach (var (key, value) in _searchers)
            {
                if (SearchResult == null)
                {
                    SearchResult = ExpressionHelper.InvokeGenericFuncWithKnownType(ApiName, SearchId, value, key);
                }
                else
                {
                    break;
                }
            }

            if (SearchResult != null)
            {
                ResponseInfo.IsSuccessful = true;
                ResponseInfo.Data = SearchResult.ToString();
            }
            else
            {
                if (_inserters.Any())
                {
                    foreach (var (key, value) in _inserters)
                    {
                        ExpressionHelper.CacheableInvokeGenericActionWithKnownType(ApiName, SearchId, value,
                            key);
                    }
                }
                else
                {
                    ResponseInfo.IsSuccessful = false;
                    ResponseInfo.ErrorMessage = $"Unable to find {_targetFieldName} in tables {string.Join(" or ", _searchers.Select(z => z.Key.Name + "s"))} with {_sourceFieldName}: {searchId}";
                }
            }

            SearchResult = null;

            return ResponseInfo;
        }

        /// <summary>
        /// Do the real search against the back end data store
        /// </summary>
        /// <typeparam name="T">Table type to be searched</typeparam>
        /// <param name="searchId">SearchId, don't use the class instance because it will be cached when we cache internal search
        /// in InvokeGenericFuncWithKnownType</param>
        /// <param name="matcher">search field expression</param>
        /// <param name="getter">return field func</param>
        /// <returns>field value from getter</returns>
        private static object InternalSearch<T>(object searchId, Expression<Func<T, object>> matcher, Func<T, object> getter) where T : class
        {
            var expression = ExpressionHelper.BuildEntityKeyExpression(searchId, matcher);
            var result = DataStore.Instance.GetTable<T>().FirstOrDefault(expression.Compile());

            object searchResult = null;
            if (result != null)
            {
                searchResult = getter(result);
            }

            return searchResult;
        }

        /// <summary>
        /// Add into the back end store if all searches failed
        /// </summary>
        /// <typeparam name="T">Table to be inserted</typeparam>
        /// <param name="searchId">SearchId, don't use the class instance because it will be cached when we cache internal search
        /// in InvokeGenericFuncWithKnownType</param>
        /// <param name="setter">expression for searchId to be inserted into table T</param>
        /// <param name="getter">return field func after insertion</param>
        private void InternalInsert<T>(object searchId, Expression<Func<T, object>> setter, Func<T, object> getter) where T : class, new()
        {
            try
            {
                var setterAction = ExpressionHelper.CreatePropertySetter(setter);
                var newRow = new T();
                setterAction(newRow, searchId);

                DataStore.Instance.AddTable(newRow);

                ResponseInfo.IsSuccessful = true;
                ResponseInfo.Data = getter(newRow).ToString();
            }
            catch (Exception ex)
            {
                ResponseInfo = new ResponseInfo()
                {
                    IsSuccessful = false,
                    ErrorMessage = ex.Message,
                    ErrorException = ex.ToString(),
                };
            }
        }
    }
}

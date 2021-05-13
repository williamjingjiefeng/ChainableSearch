# Chainable search service that can operate on different type of entities and/or tables

A search service that can chain a set of rules upon different type of entities, and later be invoked by the target search identifier.

Author:	

William Jingjie Feng:	<william.feng@vcstechnology.com>

GitHub repository:
<https://github.com/williamjingjiefeng/ChainableSearch>

It features:

•	If you define a service that supports chaining as follows:
	
    var service = new ChainableService()
        .Search<Customer>(z => z.Id, z => z.Name)
        .Search<Friend>(z => z.Id, z => z.Name)
        .Search<Colleague>(z => z.Id, z => z.Name)
        .Search<Classmate>(z => z.Id, z => z.Name)
        .Insert<Contact>(z => z.Id, z => z.Name);

	Once you call service.Action() with a Id, we will loop through all searchers defined as above, if Id is found in Customer table, customer name 
	will be returned, otherwise Friend table will be searched and upon successful matching, friend name will be retrieved. This flow will keep going 
	until all searchers have been enumerated and if no Id is matched for all tables, we will eventually insert this new entity into Contact table.

•	As you can see, all searchers and inserters are strongly typed with Lambda expression, elimination of \<object\> generic parameters has been endorsed.

•	Compact "fluent" chaining behaviour.

•	Restricted API: all the internals are private, only a small number of public methods are available. It means the API surface is simple 
	even though there is complexity underneath.

•	Maintain difficulty level of the code – there are still tons of generic type parameters happening.

Use Cases:

•	Searching a variety of business entities with similar logic is common for line of business applications.

•	It is imperative to store those searching behaviour as business rules and fire up them later when needed, instead of executing the search one by one 
    upfront.

•	Defining searching rules as chainable executables will help unify the way of both sync and async calling of action, and streamline the logic on previous 
    searching result

Implementation details are explained as follows:

1. 	The hard bit is although search() API is generic type parameterized, however, once we chain them up, it is very hard to save them in a data structure that 
	supports different type of entities. One possible solution is to make service class have those types defined for the search, however, this will construct 
    loads of same services with different number of generic arguments such as C# func, which can go up to 16, which is a bit ugly and less flexible. What we 
    have made a breakthrough is saving those search matcher and getter into a dictionary as an expression of Func or Action with the type as the dictionary key
    in the following manner:

        // define search expression so that we can invoke it later.
        Expression<Func<object, object>> searcher = z => InternalSearch(z, matcher, getter.Compile());

        if (!_searchers.ContainsKey(typeof(T)))
        {
            _searchers.Add(typeof(T), searcher);
        }
	
2.	Then at run time we loop through searchers and inserters dictionary and call InvokeGenericFuncWithKnownType() or CacheableInvokeGenericActionWithKnownType
    accordingly as follows:

        foreach (var searcher in searchers) 
        {
            if (SearchResult == null)
            {
                SearchResult = ExpressionHelper.InvokeGenericFuncWithKnownType(ApiName, SearchId, searcher.Value, searcher.Key);
            }
            else
            {
                break;
            }
        }

        foreach (var inserter in inserters) 
        {
            ExpressionHelper. and CacheableInvokeGenericActionWithKnownType(ApiName, SearchId, inserter.Value, inserter.Key);
        }

	searcher's key will be entity's type, and value will be internal search that can be invoked will typed matchers and getters.

3.	Internal search is a method with a type parameter T. You want to call the method, but you only have the type in a variable. 
    This will generate a call of the method using that type as a type parameter.

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
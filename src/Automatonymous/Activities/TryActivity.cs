// Copyright 2007-2014 Chris Patterson, Dru Sellers, Travis Smith, et. al.
//  
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use
// this file except in compliance with the License. You may obtain a copy of the 
// License at 
// 
//     http://www.apache.org/licenses/LICENSE-2.0 
// 
// Unless required by applicable law or agreed to in writing, software distributed
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR 
// CONDITIONS OF ANY KIND, either express or implied. See the License for the 
// specific language governing permissions and limitations under the License.
namespace Automatonymous.Activities
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Behaviors;
    using Internals.Caching;


    public class TryActivity<TInstance> :
        Activity<TInstance>
    {
        readonly Cache<Type, List<ExceptionActivity<TInstance>>> _exceptionHandlers;
        Behavior<TInstance> _behavior;

        public TryActivity(Event @event, IEnumerable<EventActivity<TInstance>> activities,
            IEnumerable<ExceptionActivity<TInstance>> exceptionHandlers)
        {
            _behavior = CreateBehavior(activities.Select(x => (Activity<TInstance>)new EventActivityShim<TInstance>(@event, x)).ToArray());

            _exceptionHandlers = new DictionaryCache<Type, List<ExceptionActivity<TInstance>>>(
                x => new List<ExceptionActivity<TInstance>>());

            foreach (var exceptionActivity in exceptionHandlers)
                _exceptionHandlers[exceptionActivity.ExceptionType].Add(exceptionActivity);
        }

        public void Accept(StateMachineInspector inspector)
        {
            inspector.Inspect(this, _ =>
            {
                _behavior.Accept(inspector);

                _exceptionHandlers.Each((type, handler) => handler.ForEach(x => x.Accept(inspector)));
            });
        }

        async Task Activity<TInstance>.Execute(BehaviorContext<TInstance> context, Behavior<TInstance> next)
        {
            await Execute(context);

            await next.Execute(context);
        }

        async Task Activity<TInstance>.Execute<T>(BehaviorContext<TInstance, T> context, Behavior<TInstance, T> next)
        {
            await Execute(context);

            await next.Execute(context);
        }

        Behavior<TInstance> CreateBehavior(Activity<TInstance>[] activities)
        {
            if (activities.Length == 0)
                return Behavior.Empty<TInstance>();

            Behavior<TInstance> current = new LastBehavior<TInstance>(activities[activities.Length - 1]);

            for (int i = activities.Length - 2; i >= 0; i--)
                current = new ActivityBehavior<TInstance>(activities[i], current);

            return current;
        }

        async Task Execute(BehaviorContext<TInstance> context)
        {
            Exception exception = null;

            try
            {
                await _behavior.Execute(context);
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            if (exception != null)
            {
                Type exceptionType = exception.GetType();
                while (exceptionType != typeof(Exception).BaseType && exceptionType != null)
                {
                    List<ExceptionActivity<TInstance>> handlers;
                    if (_exceptionHandlers.TryGetValue(exceptionType, out handlers))
                    {
                        foreach (var handler in handlers)
                        {
                            BehaviorContext<TInstance> contextProxy = handler.GetExceptionContext(context, exception);

                            var behavior = new LastBehavior<TInstance>(handler);

                            await behavior.Execute(contextProxy);
                        }

                        return;
                    }
                    exceptionType = exceptionType.BaseType;
                }

                throw new AutomatonymousException("The activity threw an exception", exception);
            }
        }
    }


    public class TryActivity<TInstance, TData> :
        Activity<TInstance, TData>
        where TInstance : class
    {
        readonly List<Activity<TInstance>> _activities;
        readonly Cache<Type, List<ExceptionActivity<TInstance>>> _exceptionHandlers;

        public TryActivity(Event @event, IEnumerable<EventActivity<TInstance>> activities,
            IEnumerable<ExceptionActivity<TInstance>> exceptionBinder)
        {
            _activities = new List<Activity<TInstance>>(activities
                .Select(x => new EventActivityShim<TInstance>(@event, x)));

            _exceptionHandlers = new DictionaryCache<Type, List<ExceptionActivity<TInstance>>>(
                x => new List<ExceptionActivity<TInstance>>());

            foreach (var exceptionActivity in exceptionBinder)
                _exceptionHandlers[exceptionActivity.ExceptionType].Add(exceptionActivity);
        }

        public void Accept(StateMachineInspector inspector)
        {
            inspector.Inspect(this, _ =>
            {
                _activities.ForEach(activity => activity.Accept(inspector));
            });
        }

        async Task Activity<TInstance, TData>.Execute(BehaviorContext<TInstance, TData> context, Behavior<TInstance, TData> next)
        {
            Exception exception = null;
            try
            {
                foreach (var activity in _activities)
                    await activity.Execute(context, next);
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            if (exception != null)
            {
                Type exceptionType = exception.GetType();
                while (exceptionType != typeof(Exception).BaseType && exceptionType != null)
                {
                    List<ExceptionActivity<TInstance>> handlers;
                    if (_exceptionHandlers.TryGetValue(exceptionType, out handlers))
                    {
                        foreach (var handler in handlers)
                        {
                            BehaviorContext<TInstance> contextProxy = handler.GetExceptionContext(context, exception);

                            var behavior = new LastBehavior<TInstance>(handler);

                            await behavior.Execute(contextProxy);
                        }

                        return;
                    }
                    exceptionType = exceptionType.BaseType;
                }

                throw new AutomatonymousException("The activity threw an exception", exception);
            }
        }
    }
}
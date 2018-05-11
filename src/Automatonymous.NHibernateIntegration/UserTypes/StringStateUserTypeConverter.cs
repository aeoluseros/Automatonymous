﻿// Copyright 2011-2018 Chris Patterson, Dru Sellers
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
namespace Automatonymous.UserTypes
{
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Linq;
    using NHibernate;
    using NHibernate.Engine;
    using NHibernate.SqlTypes;


    /// <summary>
    /// The default storage
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class StringStateUserTypeConverter<T> :
        StateUserTypeConverter
        where T : StateMachine
    {
        static readonly SqlType[] _types = {NHibernateUtil.String.SqlType};
        readonly Dictionary<string, State> _stateCache;

        public StringStateUserTypeConverter(T machine)
        {
            _stateCache = new Dictionary<string, State>(machine.States.ToDictionary(x => x.Name));
        }

        public SqlType[] Types
        {
            get { return _types; }
        }

        public State Get(DbDataReader rs, string[] names, ISessionImplementor session)
        {
            var value = (string)NHibernateUtil.String.NullSafeGet(rs, names, session);

            State state = _stateCache[value];

            return state;
        }

        public void Set(DbCommand command, object value, int index, ISessionImplementor session)
        {
            if (value == null)
            {
                NHibernateUtil.String.NullSafeSet(command, null, index, session);
                return;
            }

            value = ((State)value).Name;

            NHibernateUtil.String.NullSafeSet(command, value, index, session);
        }
    }
}
﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using log4net;
using NetGore.Collections;

namespace NetGore
{
    /// <summary>
    /// A manager for a collection of ISkills.
    /// </summary>
    /// <typeparam name="TSkillType">The type of skill type enum.</typeparam>
    /// <typeparam name="TStatType">The type of stat type enum.</typeparam>
    /// <typeparam name="TCharacter">The type of character.</typeparam>
    public class SkillManager<TSkillType, TStatType, TCharacter>
        where TSkillType : struct, IComparable, IConvertible, IFormattable
        where TStatType : struct, IComparable, IConvertible, IFormattable
        where TCharacter : class
    {
        static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Dictionary that allows for lookup of an ISkill for the given SkillType.
        /// </summary>
        readonly Dictionary<TSkillType, ISkill<TSkillType, TStatType, TCharacter>> _skills =
            new Dictionary<TSkillType, ISkill<TSkillType, TStatType, TCharacter>>(EnumComparer<TSkillType>.Instance);

        static Func<Type, bool> GetTypeFilter()
        {
            TypeFilterCreator filterCreator = new TypeFilterCreator
            {
                IsClass = true,
                IsAbstract = false,
                Interfaces = new Type[] { typeof(ISkill<TSkillType, TStatType, TCharacter>) },
                ConstructorParameters = Type.EmptyTypes,
                RequireConstructor = true
            };

            return filterCreator.GetFilter();
        }

        /// <summary>
        /// Handles when a new type has been loaded into a <see cref="TypeFactory"/>.
        /// </summary>
        /// <param name="typeFactory"><see cref="TypeFactory"/> that the event occured on.</param>
        /// <param name="loadedType">Type that was loaded.</param>
        /// <param name="name">Name of the Type.</param>
        void TypeFactoryLoadedHandler(TypeFactory typeFactory, Type loadedType, string name)
        {
            var instance = (ISkill<TSkillType, TStatType, TCharacter>)TypeFactory.GetTypeInstance(loadedType);

            if (_skills.ContainsKey(instance.SkillType))
            {
                const string errmsg = "Skills Dictionary already contains SkillType `{0}`.";
                Debug.Fail(string.Format(errmsg, instance.SkillType));
                if (log.IsFatalEnabled)
                    log.FatalFormat(errmsg, instance.SkillType);
                throw new Exception(string.Format(errmsg, instance.SkillType));
            }

            _skills.Add(instance.SkillType, instance);

            if (log.IsInfoEnabled)
                log.InfoFormat("Created skill object for SkillType `{0}`.", instance.SkillType);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SkillManager&lt;TSkillType, TStatType, TCharacter&gt;"/> class.
        /// </summary>
        public SkillManager()
        {
            new TypeFactory(GetTypeFilter(), TypeFactoryLoadedHandler);
        }

        /// <summary>
        /// Gets the ISkill for the given skill type.
        /// </summary>
        /// <param name="skillType">The skill type to get the ISkill for.</param>
        /// <returns>The ISkill for the given <paramref name="skillType"/>, or null if the <paramref name="skillType"/>
        /// is invalid or contains no ISkill.</returns>
        public ISkill<TSkillType, TStatType, TCharacter> GetSkill(TSkillType skillType)
        {
            ISkill<TSkillType, TStatType, TCharacter> value;
            if (!_skills.TryGetValue(skillType, out value))
            {
                const string errmsg = "Failed to get the SkillBase for SkillType `{0}`.";
                if (log.IsWarnEnabled)
                    log.WarnFormat(errmsg, skillType);
                Debug.Fail(string.Format(errmsg, skillType));
                return null;
            }

            return value;
        }
    }
}

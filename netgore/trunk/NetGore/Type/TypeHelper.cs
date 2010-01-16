using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using log4net;

namespace NetGore
{
    /// <summary>
    /// Provides helper functions for the Type class.
    /// </summary>
    public static class TypeHelper
    {
        static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        const bool _gacDefault = false;

        /// <summary>
        /// Gets all Types from every Assembly.
        /// </summary>
        /// <param name="includeGAC">If true, Types from Assemblies from the Global Assembly Cache will be included.
        /// Default is false.</param>
        /// <returns>All Types from every Assembly.</returns>
        public static IEnumerable<Type> AllTypes(bool includeGAC)
        {
            IEnumerable<Assembly> assemblies = AppDomain.CurrentDomain.GetAssemblies();
            if (!includeGAC)
                assemblies = assemblies.Where(x => !x.GlobalAssemblyCache);
            var types = assemblies.SelectMany(x => x.GetTypes());
            return types;
        }

        /// <summary>
        /// Gets all Types from every Assembly.
        /// </summary>
        /// <returns>All Types from every Assembly.</returns>
        public static IEnumerable<Type> AllTypes()
        {
            return AllTypes(_gacDefault);
        }

        /// <summary>
        /// Finds all Types that match the given conditions.
        /// </summary>
        /// <param name="conditions">The conditions that each Type must meet.</param>
        /// <param name="constructorParams">An array of Types that define the parameters required for
        /// the constructor. If no constructor with the specified parameter Types are found, a
        /// MissingMethodException will be thrown. Set to null to not require any constructor, and set to
        /// an empty array to require an empty constructor. Use this parameter only if you want to require
        /// a specific constructor signature, not if you just want to find Types that have a specific
        /// constructor signature. If this value is not null, only instanceable classes will be returned.</param>
        /// <returns>All Types that match the given conditions.</returns>
        public static IEnumerable<Type> FindTypes(Func<Type, bool> conditions, Type[] constructorParams)
        {
            return FindTypes(conditions, constructorParams, _gacDefault);
        }

        /// <summary>
        /// Finds all Types that match the given conditions.
        /// </summary>
        /// <param name="conditions">The conditions that each Type must meet.</param>
        /// <param name="constructorParams">An array of Types that define the parameters required for
        /// the constructor. If no constructor with the specified parameter Types are found, a
        /// MissingMethodException will be thrown. Set to null to not require any constructor, and set to
        /// an empty array to require an empty constructor. Use this parameter only if you want to require
        /// a specific constructor signature, not if you just want to find Types that have a specific
        /// constructor signature. If this value is not null, only instanceable classes will be returned.</param>
        /// <param name="includeGAC">If true, Types from Assemblies from the Global Assembly Cache will be included.
        /// Default is false.</param>
        /// <returns>All Types that match the given conditions.</returns>
        public static IEnumerable<Type> FindTypes(Func<Type, bool> conditions, Type[] constructorParams, bool includeGAC)
        {
            // Grab all types
            var types = AllTypes(includeGAC);

            // Match against the custom conditions
            if (conditions != null)
                types = types.Where(conditions);

            // Check for the required constructor
            if (constructorParams != null)
            {
                // If we are using the required constructor, only select instanceable classes
                types = types.Where(x => x.IsClass && !x.IsAbstract);

                // Make sure all the remaining Types have the required constructor
                foreach (Type type in types)
                {
                    if (!type.IsAbstract && !HasConstructorWithParameters(type, constructorParams))
                    {
                        const string errmsg = "Type `{0}` does not contain a constructor with the specified parameters.";
                        string err = string.Format(errmsg, type);
                        Debug.Fail(err);
                        if (log.IsFatalEnabled)
                            log.Fatal(err);
                        throw new MissingMethodException(err);
                    }
                }
            }

            return types;
        }

        /// <summary>
        /// Finds all Types that inherit the specified <paramref name="baseType"/>.
        /// </summary>
        /// <param name="baseType">Type of the base class or interface to find the Types that inherit.</param>
        /// <param name="constructorParams">An array of Types that define the parameters required for
        /// the constructor. If no constructor with the specified parameter Types are found, a
        /// MissingMethodException will be thrown. Set to null to not require any constructor, and set to
        /// an empty array to require an empty constructor. Use this parameter only if you want to require
        /// a specific constructor signature, not if you just want to find Types that have a specific
        /// constructor signature. If this value is not null, only instanceable classes will be returned.</param>
        /// <returns>IEnumerable of all Types that inherit the specified <paramref name="baseType"/>.</returns>
        public static IEnumerable<Type> FindTypesThatInherit(Type baseType, Type[] constructorParams)
        {
            return FindTypesThatInherit(baseType, constructorParams, _gacDefault);
        }

        /// <summary>
        /// Finds all Types that inherit the specified <paramref name="baseType"/>.
        /// </summary>
        /// <param name="baseType">Type of the base class or interface to find the Types that inherit.</param>
        /// <param name="constructorParams">An array of Types that define the parameters required for
        /// the constructor. If no constructor with the specified parameter Types are found, a
        /// MissingMethodException will be thrown. Set to null to not require any constructor, and set to
        /// an empty array to require an empty constructor. Use this parameter only if you want to require
        /// a specific constructor signature, not if you just want to find Types that have a specific
        /// constructor signature. If this value is not null, only instanceable classes will be returned.</param>
        /// <param name="includeGAC">If true, Types from Assemblies from the Global Assembly Cache will be included.
        /// Default is false.</param>
        /// <returns>IEnumerable of all Types that inherit the specified <paramref name="baseType"/>.</returns>
        public static IEnumerable<Type> FindTypesThatInherit(Type baseType, Type[] constructorParams, bool includeGAC)
        {
            return FindTypes(x => x.IsSubclassOf(baseType), constructorParams, includeGAC);
        }

        /// <summary>
        /// Finds all Types that contain an attribute of Type <paramref name="attributeType"/>.
        /// </summary>
        /// <param name="attributeType">Type of the attribute to find on the Types.</param>
        /// <param name="constructorParams">An array of Types that define the parameters required for
        /// the constructor. If no constructor with the specified parameter Types are found, a
        /// MissingMethodException will be thrown. Set to null to not require any constructor, and set to
        /// an empty array to require an empty constructor. Use this parameter only if you want to require
        /// a specific constructor signature, not if you just want to find Types that have a specific
        /// constructor signature. If this value is not null, only instanceable classes will be returned.</param>
        /// <returns>IEnumerable of all Types that have the specified <paramref name="attributeType"/>.</returns>
        public static IEnumerable<Type> FindTypesWithAttribute(Type attributeType, Type[] constructorParams)
        {
            return FindTypesWithAttribute(attributeType, constructorParams, _gacDefault);
        }

        /// <summary>
        /// Finds all Types that contain an attribute of Type <paramref name="attributeType"/>.
        /// </summary>
        /// <param name="attributeType">Type of the attribute to find on the Types.</param>
        /// <param name="constructorParams">An array of Types that define the parameters required for
        /// the constructor. If no constructor with the specified parameter Types are found, a
        /// MissingMethodException will be thrown. Set to null to not require any constructor, and set to
        /// an empty array to require an empty constructor. Use this parameter only if you want to require
        /// a specific constructor signature, not if you just want to find Types that have a specific
        /// constructor signature. If this value is not null, only instanceable classes will be returned.</param>
        /// <param name="includeGAC">If true, Types from Assemblies from the Global Assembly Cache will be included.
        /// Default is false.</param>
        /// <returns>IEnumerable of all Types that have the specified <paramref name="attributeType"/>.</returns>
        public static IEnumerable<Type> FindTypesWithAttribute(Type attributeType, Type[] constructorParams, bool includeGAC)
        {
            Func<Type, bool> func = (x => x.GetCustomAttributes(attributeType, true).Count() > 0);
            return FindTypes(func, constructorParams, includeGAC);
        }

        /// <summary>
        /// Gets all of the custom attributes for a PropertyInfo, including those attached to
        /// abstract Properties on base classes.
        /// </summary>
        /// <typeparam name="T">The Type of attribute to find.</typeparam>
        /// <param name="propInfo">PropertyInfo to get the custom attributes for.</param>
        /// <param name="flags">BindingFlags to use for finding the PropertyInfos.</param>
        /// <returns>
        /// An IEnumerable of all of the custom attributes for a PropertyInfo.
        /// </returns>
        public static IEnumerable<T> GetAllCustomAttributes<T>(PropertyInfo propInfo, BindingFlags flags) where T : Attribute
        {
            return InternalGetAllCustomAttributes<T>(propInfo, propInfo.DeclaringType, flags).Distinct();
        }

        static bool HasConstructorWithParameters(Type type, Type[] expected)
        {
            const BindingFlags bf = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

            foreach (ConstructorInfo constructor in type.GetConstructors(bf))
            {
                var actual = constructor.GetParameters().Select(x => x.ParameterType).ToArray();

                if (expected.Length != actual.Length)
                    continue;

                for (int i = 0; i < expected.Length; i++)
                {
                    if (expected[i] != actual[i])
                        continue;
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets all of the custom attributes for a PropertyInfo, including those attached to interfaces and abstract
        /// Properties on base classes.
        /// </summary>
        /// <typeparam name="T">The Type of attribute to find.</typeparam>
        /// <param name="propInfo">PropertyInfo to get the custom attributes for.</param>
        /// <param name="type">Type of the class to search for the PropertyInfos in.</param>
        /// <param name="flags">BindingFlags to use for finding the PropertyInfos.</param>
        /// <returns>An IEnumerable of all of the custom attributes for a PropertyInfo.</returns>
        static IEnumerable<T> InternalGetAllCustomAttributes<T>(PropertyInfo propInfo, Type type, BindingFlags flags)
            where T : Attribute
        {
            // Get the attributes for this type
            var customAttributes = propInfo.GetCustomAttributes(typeof(T), false).OfType<T>();
            foreach (var attrib in customAttributes)
            {
                yield return attrib;
            }

            // Get the base type
            Type baseType = type.BaseType;

            // Get the property for the base type
            PropertyInfo baseProp = baseType.GetProperty(propInfo.Name, flags);

            // If the property for the base type exists, find the attributes for it
            if (baseProp != null)
            {
                var baseAttributes = InternalGetAllCustomAttributes<T>(baseProp, baseType, flags);
                foreach (var attrib in baseAttributes)
                {
                    yield return attrib;
                }
            }
        }

        /// <summary>
        /// Gets if the <paramref name="type"/> is public through the whole declaration tree. For example, a nested
        /// class will only return true if it is public, and so is the class it is nested in. Any non-public
        /// type in the hierarchy will result in a false value.
        /// </summary>
        /// <param name="type">The type to check.</param>
        /// <returns>True if the <paramref name="type"/> is a class and public through the whole declaration tree.</returns>
        public static bool IsClassTypeTreePublic(Type type)
        {
            if (!type.IsClass)
                return false;

            if (type.IsNested)
            {
                if (!type.IsNestedPublic)
                    return false;

                return IsClassTypeTreePublic(type.DeclaringType);
            }
            else
                return type.IsPublic;
        }
    }
}
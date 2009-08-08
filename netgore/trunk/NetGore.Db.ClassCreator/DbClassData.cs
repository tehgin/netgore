﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetGore.Db.ClassCreator.Properties;

namespace NetGore.Db.ClassCreator
{
    /// <summary>
    /// Contains the data used for generating a class for a database table.
    /// </summary>
    public class DbClassData
    {
        public readonly string ClassName;
        public readonly IEnumerable<ColumnCollection> ColumnCollections;
        public readonly IEnumerable<DbColumnInfo> Columns;
        public readonly CodeFormatter Formatter;
        public readonly string InterfaceName;
        public readonly string TableName;
        readonly IEnumerable<CustomTypeMapping> _customTypes;
        readonly Dictionary<Type, string> _dataReaderReadMethods;

        readonly IDictionary<DbColumnInfo, string> _parameterNames = new Dictionary<DbColumnInfo, string>();
        readonly IDictionary<DbColumnInfo, string> _privateNames = new Dictionary<DbColumnInfo, string>();
        readonly IDictionary<DbColumnInfo, string> _publicNames = new Dictionary<DbColumnInfo, string>();

        readonly IDictionary<DbColumnInfo, string> _externalTypes = new Dictionary<DbColumnInfo, string>();

        public DbClassData(string tableName, IEnumerable<DbColumnInfo> columns, CodeFormatter formatter,
                           Dictionary<Type, string> dataReaderReadMethods, IEnumerable<ColumnCollection> columnCollections,
                           IEnumerable<CustomTypeMapping> customTypes)
        {
            const string tableNameWildcard = "*";

            TableName = tableName;
            Columns = columns;
            Formatter = formatter;
            _dataReaderReadMethods = dataReaderReadMethods;

            ClassName = formatter.GetClassName(tableName);
            InterfaceName = formatter.GetInterfaceName(tableName);

            // Custom types filter
            _customTypes =
                customTypes.Where(
                    x =>
                    x.Columns.Count() > 0 &&
                    (x.Tables.Contains(TableName, StringComparer.OrdinalIgnoreCase) || x.Tables.Contains(tableNameWildcard))).
                    ToArray();

            // Column collections filter
            ColumnCollections =
                columnCollections.Where(
                    x =>
                    x.Columns.Count() > 0 &&
                    (x.Tables.Contains(TableName, StringComparer.OrdinalIgnoreCase) || x.Tables.Contains(tableNameWildcard))).
                    ToArray();

            // Populate the external types dictionary
            foreach (DbColumnInfo column in columns)
            {
                string columnName = column.Name;
                string externalType;
                var customType = _customTypes.FirstOrDefault(x => x.Columns.Contains(columnName, StringComparer.OrdinalIgnoreCase));
                if (customType != null)
                    externalType = customType.CustomType;
                else
                    externalType = GetInternalType(column);

                _externalTypes.Add(column, externalType);
            }

            // Populate the naming dictionaries
            foreach (DbColumnInfo column in columns)
            {
                _privateNames.Add(column, formatter.GetFieldName(column.Name, MemberVisibilityLevel.Private, GetInternalType(column)));
                _publicNames.Add(column, formatter.GetFieldName(column.Name, MemberVisibilityLevel.Public, GetExternalType(column)));
                _parameterNames.Add(column, formatter.GetParameterName(column.Name, column.Type));
            }
        }

        /// <summary>
        /// Gets the ColumnCollection for a given DbColumnInfo, or null if the DbColumnInfo is not part of
        /// andy ColumnCollection in this table.
        /// </summary>
        /// <param name="dbColumn">The DbColumnInfo to get the ColumnCollection for.</param>
        /// <returns>The ColumnCollection the DbColumnInfo is part of, or null if it
        /// is not part of a ColumnCollection.</returns>
        public ColumnCollection GetCollectionForColumn(DbColumnInfo dbColumn)
        {
            ColumnCollectionItem item;
            return GetCollectionForColumn(dbColumn, out item);
        }

        /// <summary>
        /// Gets the ColumnCollection for a given DbColumnInfo, or null if the DbColumnInfo is not part of
        /// andy ColumnCollection in this table.
        /// </summary>
        /// <param name="dbColumn">The DbColumnInfo to get the ColumnCollection for.</param>
        /// <<param name="item">The ColumnCollectionItem for the <paramref name="dbColumn"/> in the ColumnCollection.</param>
        /// <returns>The ColumnCollection the DbColumnInfo is part of, or null if it
        /// is not part of a ColumnCollection.</returns>
        public ColumnCollection GetCollectionForColumn(DbColumnInfo dbColumn, out ColumnCollectionItem item)
        {
            foreach (ColumnCollection columnCollection in ColumnCollections)
            {
                var matches =
                    columnCollection.Columns.Where(x => x.ColumnName.Equals(dbColumn.Name, StringComparison.OrdinalIgnoreCase));
                int count = matches.Count();
                if (count == 1)
                {
                    item = matches.First();
                    return columnCollection;
                }
                else if (count > 1)
                {
                    throw new Exception(
                        string.Format("DbColumnInfo for column `{0}` in table `{1}` matched more than one ColumnCollection!",
                                      dbColumn.Name, TableName));
                }
            }

            item = default(ColumnCollectionItem);
            return null;
        }

        public static string GetCollectionTypeString(ColumnCollection columnCollection)
        {
            return GetConstEnumDictonaryName(columnCollection);
        }

        /// <summary>
        /// Gets the code to use for the accessor for a DbColumnInfo.
        /// </summary>
        /// <param name="dbColumn">The DbColumnInfo to get the value accessor for.</param>
        /// <returns>The code to use for the accessor for a DbColumnInfo.</returns>
        public string GetColumnValueAccessor(DbColumnInfo dbColumn)
        {
            ColumnCollectionItem item;
            ColumnCollection coll = GetCollectionForColumn(dbColumn, out item);

            if (coll == null)
            {
                // Not part of a collection
                return GetPublicName(dbColumn);
            }
            else
            {
                // Part of a collection
                StringBuilder sb = new StringBuilder();
                sb.Append("Get" + GetPublicName(coll));
                sb.Append(Formatter.OpenParameterString);
                sb.Append(Formatter.GetCast(coll.KeyType));
                sb.Append(item.Key);
                sb.Append(Formatter.CloseParameterString);
                return sb.ToString();
            }
        }

        /// <summary>
        /// Gets the code to use for the mutator for a DbColumnInfo.
        /// </summary>
        /// <param name="dbColumn">The DbColumnInfo to get the value mutator for.</param>
        /// <param name="valueName">Code to generate for the value to set.</param>
        /// <returns>The code to use for the mutator for a DbColumnInfo.</returns>
        public string GetColumnValueMutator(DbColumnInfo dbColumn, string valueName)
        {
            ColumnCollectionItem item;
            ColumnCollection coll = GetCollectionForColumn(dbColumn, out item);

            StringBuilder sb = new StringBuilder();

            if (coll == null)
            {
                // Not part of a collection
                sb.Append(GetPublicName(dbColumn));
                sb.Append(" = ");
                sb.Append(Formatter.GetCast(GetExternalType(dbColumn)));
                sb.Append(valueName);
                sb.Append(Formatter.EndOfLine);
            }
            else
            {
                // Part of a collection
                sb.Append("Set" + GetPublicName(coll));
                sb.Append(Formatter.OpenParameterString);
                sb.Append(Formatter.GetCast(coll.KeyType));
                sb.Append(item.Key);
                sb.Append(Formatter.ParameterSpacer);
                sb.Append(Formatter.GetCast(coll.ValueType));
                sb.Append(valueName);
                sb.Append(Formatter.CloseParameterString);
                sb.Append(Formatter.EndOfLine);
            }

            return sb.ToString();
        }

        public string GetConstEnumDictonaryCode(ColumnCollection columnCollection)
        {
            StringBuilder sb = new StringBuilder(Resources.ConstEnumDictionaryCode);
            sb.Replace("[CLASSNAME]", GetConstEnumDictonaryName(columnCollection));
            sb.Replace("[VALUETYPE]", Formatter.GetTypeString(columnCollection.ValueType));
            sb.Replace("[KEYTYPE]", Formatter.GetTypeString(columnCollection.KeyType));
            sb.Replace("[COLUMNCOLLECTIONNAME]", columnCollection.Name);
            sb.Replace("[STORAGETYPE]", Formatter.GetTypeString(typeof(int)));
            return sb.ToString();
        }

        public static string GetConstEnumDictonaryName(ColumnCollection columnCollection)
        {
            return columnCollection.Name.Substring(0, 1).ToUpper() + columnCollection.Name.Substring(1) + "ConstDictionary";
        }

        /// <summary>
        /// Gets the code string used for accessing a database DbColumnInfo's value from a DataReader.
        /// </summary>
        /// <param name="column">The DbColumnInfo to get the value from.</param>
        /// <param name="ordinalFieldName">Name of the local field used to store the ordinal. The ordinal
        /// must already be assigned to this field.</param>
        /// <returns>The code string used for accessing a database DbColumnInfo's value.</returns>
        public string GetDataReaderAccessor(DbColumnInfo column, string ordinalFieldName)
        {
            string callMethod = GetDataReaderReadMethodName(column.Type);

            // Find the method to use for reading the value
            StringBuilder sb = new StringBuilder();

            // Cast
            sb.Append(Formatter.GetCast(GetExternalType(column)));

            // Accessor
            if (column.Type.IsNullable())
            {
                sb.Append(Formatter.OpenParameterString);
                sb.Append(DbClassGenerator.DataReaderName);
                sb.Append(".IsDBNull");
                sb.Append(Formatter.OpenParameterString);
                sb.Append(ordinalFieldName);
                sb.Append(Formatter.CloseParameterString);
                sb.Append(" ? ");
                sb.Append(Formatter.GetCast(column.Type));
                sb.Append("null : ");
            }
            sb.Append(DbClassGenerator.DataReaderName);
            sb.Append(".");
            sb.Append(callMethod);
            sb.Append(Formatter.OpenParameterString);
            sb.Append(ordinalFieldName);
            sb.Append(Formatter.CloseParameterString);
            
            if (column.Type.IsNullable())
                sb.Append(Formatter.CloseParameterString);

            return sb.ToString();
        }

        /// <summary>
        /// Gets the name of the method used by the DataReader to read the given Type.
        /// </summary>
        /// <param name="type">Type to read.</param>
        /// <returns>The name of the method used by the DataReader to read the given Type.</returns>
        public string GetDataReaderReadMethodName(Type type)
        {
            if (type.IsNullable())
                type = type.GetNullableUnderlyingType();

            string callMethod;
            if (_dataReaderReadMethods.TryGetValue(type, out callMethod))
                return callMethod;

            return "Get" + type.Name;
        }

        /// <summary>
        /// Gets the parameter name for a DbColumnInfo.
        /// </summary>
        /// <param name="dbColumn">The DbColumnInfo to get the parameter name for.</param>
        /// <returns>The parameter name for the DbColumnInfo.</returns>
        public string GetParameterName(DbColumnInfo dbColumn)
        {
            return _parameterNames[dbColumn];
        }

        /// <summary>
        /// Gets the private name for a DbColumnInfo.
        /// </summary>
        /// <param name="dbColumn">The DbColumnInfo to get the private name for.</param>
        /// <returns>The private name for the DbColumnInfo.</returns>
        public string GetPrivateName(DbColumnInfo dbColumn)
        {
            return _privateNames[dbColumn];
        }

        public string GetPrivateName(ColumnCollection columnCollection)
        {
            return Formatter.GetFieldName(columnCollection.Name, MemberVisibilityLevel.Private, columnCollection.ValueType);
        }

        /// <summary>
        /// Gets the public name for a DbColumnInfo.
        /// </summary>
        /// <param name="dbColumn">The DbColumnInfo to get the public name for.</param>
        /// <returns>The public name for the DbColumnInfo.</returns>
        public string GetPublicName(DbColumnInfo dbColumn)
        {
            return _publicNames[dbColumn];
        }

        /// <summary>
        /// Gets a string for the Type used externally for a given column.
        /// </summary>
        /// <param name="dbColumn">The DbColumnInfo to get the external type for.</param>
        /// <returns>A string for the Type used externally for a given column.</returns>
        public string GetExternalType(DbColumnInfo dbColumn)
        {
            return _externalTypes[dbColumn];
        }

        /// <summary>
        /// Gets a string for the Type used internally for a given column.
        /// </summary>
        /// <param name="dbColumn">The DbColumnInfo to get the internal type for.</param>
        /// <returns>A string for the Type used internally for a given column.</returns>
        public string GetInternalType(DbColumnInfo dbColumn)
        {
            return Formatter.GetTypeString(dbColumn.Type);
        }

        public string GetPublicName(ColumnCollection columnCollection)
        {
            if (columnCollection.Name != "Stat")
            {
            }

            return Formatter.GetFieldName(columnCollection.Name, MemberVisibilityLevel.Public, columnCollection.ValueType);
        }
    }
}
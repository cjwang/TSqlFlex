﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.SqlServer.Types;

namespace TSqlFlex.Core
{
    public static class FieldScripting
    {
        private static Encoding codePage1252Encoding = Encoding.GetEncoding(1252);

        public enum ADONetFieldInfo : int
        {
            Name = 0,
            ColumnSize = 2,
            NumericPrecision = 3,
            NumericScale = 4,
            BaseTableName = 11,
            AllowsNulls = 13,
            IsHidden = 20,
            DataType = 24
        }

        private static CultureInfo englishUSCulture = new CultureInfo("en-US");  //default culture for formatting.

        public static string DataTypeName(SQLColumn fieldInfo)
        {
            if (fieldInfo.DataType == "real")
            {
                return "float";  //this could be a float or a real.  There is no simple way to tell via ado.net.  Will try to keep it consistent with float.
            }
            else if (fieldInfo.DataType.EndsWith(".sys.hierarchyid"))
            {
                return "hierarchyid";
            }
            else if (fieldInfo.DataType.EndsWith(".sys.geography"))
            {
                return "geography";
            }
            else if (fieldInfo.DataType.EndsWith(".sys.geometry"))
            {
                return "geometry";
            }
            return fieldInfo.DataType;
        }

        public static string DataTypeParameterIfAny(SQLColumn fieldInfo)
        {
            if (fieldInfo.DataType == "nvarchar" || fieldInfo.DataType == "varchar" || fieldInfo.DataType == "nchar" || fieldInfo.DataType == "char" || fieldInfo.DataType == "binary" || fieldInfo.DataType == "varbinary")
            {
                if (fieldInfo.ColumnSize == Int32.MaxValue)
                {
                    return "(MAX)";
                }
                return "(" + fieldInfo.ColumnSize.ToString() + ")";
            }
            else if (fieldInfo.DataType == "numeric" || fieldInfo.DataType == "decimal")
            {
                return "(" + fieldInfo.NumericPrecision.ToString() + "," + fieldInfo.NumericScale.ToString() + ")";
            }
            else if (fieldInfo.DataType == "real")
            {
                return "(24)";
            }
            else if (fieldInfo.DataType == "float")
            {
                //from MSDN: SQL Server treats n as one of two possible values. If 1<=n<=24, n is treated as 24. If 25<=n<=53, n is treated as 53.
                return "(53)";
            }
            else if (fieldInfo.DataType == "datetimeoffset" || fieldInfo.DataType == "time")
            {
                //see: http://msdn.microsoft.com/en-us/library/bb630289.aspx

                if (fieldInfo.NumericScale <= 2)
                {
                    return "(2)";
                }
                if (fieldInfo.NumericScale <= 4)
                {
                    return "(4)";
                }
                return "";
            }
            return "";
        }

        //todo: try to eliminate the .ToString() in here.
        public static string NullOrNotNull(Object allowDbNull)
        {
            if (allowDbNull is bool)
            {
                if ((bool)allowDbNull)
                {
                    return "NULL";
                }
                return "NOT NULL";
            }
            bool allowDBNullFlag;
            Type t = allowDbNull.GetType();
            if (bool.TryParse(allowDbNull.ToString(), out allowDBNullFlag))
            {
                if (allowDBNullFlag)
                {
                    return "NULL";
                }
                return "NOT NULL";
            }
            return "NULL"; //safer default for our purposes.  This is unlikely to be hit anyway.
        }

        public static string FieldNameOrDefault(SQLColumn column, int fieldIndex)
        {
            if (String.IsNullOrEmpty(column.ColumnName))
            {
                return "anonymousColumn" + (fieldIndex + 1).ToString();
            }
            return EscapeObjectName(column.ColumnName);
        }

        public static string EscapeObjectName(string rawObjectName)
        {
            if (TSqlRules.IsReservedWord(rawObjectName) || TSqlRules.ContainsWhitespace(rawObjectName) || TSqlRules.ContainsSquareBracket(rawObjectName))
            {
                return "[" + rawObjectName.Replace("]", "]]") + "]";
            }
            return rawObjectName;
        }

        public static string EscapeObjectNames(string dotSeparatedRawObjectNames)
        {
            var items = dotSeparatedRawObjectNames.Split('.');
            for (int i = 0; i < items.Length; i += 1 )
            {
                if (!IsBracketEscaped(items[i])) { 
                    items[i] = EscapeObjectName(items[i]);
                }
            }
            return string.Join(".", items);
        }

        private static bool IsBracketEscaped(string objectName)
        {
            return (objectName.StartsWith("[") && objectName.EndsWith("]"));  //bug: this is good enough for now, but may not properly consider edge case escaped ] as the final character for example
        }

        public static StringBuilder scriptDataAsInsertForSQL2008Plus(string tableName, FlexResult result, int MaxRowsInValuesClause)
        {
            const int INITIAL_CAPACITY = 50000; //This is small enough that it won't matter, but big enough to ensure minimal initial resizes.
            int calibrateBufferCapacityAfterRow = 0;

            List<SQLColumn> schema = result.schema;
            List<object[]> data = result.data;

            int columnCount = result.visibleColumnCount;
            int rowCount = data.Count;

            StringBuilder buffer = new StringBuilder(INITIAL_CAPACITY);

            for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                if (rowIndex % MaxRowsInValuesClause == 0)
                {
                    buffer.Append("INSERT INTO " + tableName + " VALUES\r\n");
                }
                buffer.Append(" (");
                for (int columnIndex = 0; columnIndex < columnCount; columnIndex++)
                {
                    buffer.Append(valueAsTSQLLiteral(data[rowIndex][columnIndex], schema[columnIndex]));
                    if (columnIndex + 1 < columnCount)
                    {
                        buffer.Append(",");
                    }
                }
                if (rowIndex + 1 == rowCount || (rowIndex + 1) % MaxRowsInValuesClause == 0)
                {
                    buffer.Append(");\r\n\r\n");
                }
                else
                {
                    buffer.Append("),\r\n");
                }

                if (rowIndex == calibrateBufferCapacityAfterRow && rowIndex + 1 < rowCount)
                {
                    //We are going to attempt to ensure there is an appropriate capacity to
                    // minimize the number of capacity adjustments required.

                    const int CHARACTERS_IN_INSERT_BLOCK = 27;
                    const double TEN_PERCENT_SLACK = 1.1;

                    int singleInsertLineLength = CHARACTERS_IN_INSERT_BLOCK + tableName.Length;
                    int averageDataRowLength = (buffer.Length - singleInsertLineLength) / (rowIndex + 1);

                    int totalInsertLineLengths = singleInsertLineLength * rowCount / 100;
                    int totalDataRowLengths = (Int32)(averageDataRowLength * rowCount * TEN_PERCENT_SLACK);

                    int newCapacity = totalInsertLineLengths + totalDataRowLengths;

                    if (newCapacity > buffer.Capacity)
                    {
                        buffer.EnsureCapacity(newCapacity);
                    }

                    //re-evaluate after the first "calibrateBufferCapacityAfterRow" rows to hopefully get a better average.
                    if (calibrateBufferCapacityAfterRow == 0)
                    {
                        calibrateBufferCapacityAfterRow = 99;
                    }
                    else if (calibrateBufferCapacityAfterRow == 99)
                    {
                        calibrateBufferCapacityAfterRow = 999;
                    }
                    else if (calibrateBufferCapacityAfterRow == 999)
                    {
                        calibrateBufferCapacityAfterRow = 9999;
                    }
                    else
                    {
                        calibrateBufferCapacityAfterRow = -1; //no more re-evaluations.
                    }
                }
            }

            return buffer;
        }

        //todo: may need some refactoring :-)
        public static string valueAsTSQLLiteral(object data, SQLColumn fieldInfo, bool forTSQLScript = true)
        {
            if (data == null || data is DBNull)
            {
                return "NULL";
            }

            if (data is string)
            {
                if ((data as string).Contains('\0'))
                {
                    return formatVarbinary(data);
                }
            }

            if (fieldInfo.DataType == "char")
            {
                return formatChar(data, forTSQLScript);
            }
            else if (fieldInfo.DataType == "varchar" || fieldInfo.DataType == "text")
            {
                return formatVarchar(data, forTSQLScript);
            }
            else if (fieldInfo.DataType == "nchar")
            {
                return formatNchar(data, forTSQLScript);
            }
            else if (fieldInfo.DataType == "nvarchar" || fieldInfo.DataType == "ntext" || fieldInfo.DataType == "xml")
            {
                return formatNvarchar(data, forTSQLScript);
            }
            else if (fieldInfo.DataType == "bigint" || fieldInfo.DataType == "numeric" || fieldInfo.DataType == "smallint" || fieldInfo.DataType == "decimal" || fieldInfo.DataType == "smallmoney" ||
                fieldInfo.DataType == "int" || fieldInfo.DataType == "tinyint" || fieldInfo.DataType == "float" || fieldInfo.DataType == "real" || fieldInfo.DataType == "money")
            {
                return getDataAsAppropriateNumericFormat(data);
            }
            else if (fieldInfo.DataType == "binary" || fieldInfo.DataType == "rowversion" || fieldInfo.DataType == "timestamp")
            {
                return formatBinary(data, fieldInfo.ColumnSize);
            }
            else if (fieldInfo.DataType == "date")
            {
                return formatDate(data, forTSQLScript);
            }
            else if (fieldInfo.DataType == "datetimeoffset")
            {
                return formatDatetimeoffset(data, forTSQLScript);
            }
            else if (fieldInfo.DataType == "datetime2")
            {
                return formatDatetime2(data, forTSQLScript);
            }
            else if (fieldInfo.DataType == "time")
            {
                return formatTime(data, forTSQLScript);
            }
            else if (fieldInfo.DataType == "datetime")
            {
                return formatDateTime(data, forTSQLScript);
            }
            else if (fieldInfo.DataType == "smalldatetime")
            {
                return formatSmallDateTime(data, forTSQLScript);
            }
            else if (fieldInfo.DataType == "bit")
            {
                return formatBit(data);
            }
            else if (fieldInfo.DataType == "varbinary" || fieldInfo.DataType == "image")
            {
                return formatVarbinary(data);
            }
            else if (fieldInfo.DataType == "uniqueidentifier")
            {
                return formatGuid(data, forTSQLScript);
            }
            else if (fieldInfo.DataType == "sql_variant")
            {
                return getDataAsSql_variantFormat(data, forTSQLScript);
            }
            else if (fieldInfo.DataType.EndsWith("hierarchyid"))
            {
                return formatHierarchyId(data);
            }
            else if (fieldInfo.DataType.EndsWith("geography"))
            {
                return formatGeography(data, forTSQLScript);
            }
            else if (fieldInfo.DataType.EndsWith("geometry"))
            {
                return formatGeometry(data, forTSQLScript);
            }
            //shouldn't get here.  In-place for future data type compatibility.
            if (data is string)
            {
                return String.Format("{0}{1}{2}",
                    (forTSQLScript ? "N'" : ""),
                        ((string)data).Replace("'", "''"),
                        (forTSQLScript ? "'" : ""));
            }
            return String.Format("{0}{1}{2}",
                    (forTSQLScript ? "N'" : ""),
                        data.ToString(),
                        (forTSQLScript ? "'" : ""));
        }

        public static string formatDecimal(object data)
        {
            decimal theDec = (decimal)data;
            if (partAfterDecimal(theDec) == 0)
            {
                return theDec.ToString("F0");
            }
            return theDec.ToString("G", englishUSCulture).TrimEnd('0');
        }

        public static string getDataAsAppropriateNumericFormat(object data)
        {
            if (data is decimal)
            {
                return formatDecimal(data);
            }
            else if (data is Double)
            {
                return formatDouble(data);
            }
            else if (data is Single)
            {
                return formatSingle(data);
            }

            return data.ToString();
        }

        public static string formatSingle(object data)
        {
            Single theSingle = (Single)data;
            if (partAfterDecimal(theSingle) == 0)
            {
                return theSingle.ToString("F0");
            }
            return theSingle.ToString("F7", englishUSCulture).TrimEnd('0');
        }

        public static string formatDouble(object data)
        {
            Double theDbl = (Double)data;
            if (partAfterDecimal(theDbl) == 0)
            {
                return theDbl.ToString("F0");
            }
            return theDbl.ToString("F7", englishUSCulture).TrimEnd('0');
        }

        public static double partAfterDecimal(Single theSingle)
        {
            return theSingle - Math.Truncate(theSingle);
        }

        public static double partAfterDecimal(Double theDbl)
        {
            return theDbl - Math.Truncate(theDbl);
        }

        public static decimal partAfterDecimal(decimal theDec)
        {
            return theDec - Math.Truncate(theDec);
        }

        public static string getDataAsSql_variantFormat(object data, bool forTSQLScript = true)
        {
            //SQL-CLR Type Mapping documentation: http://msdn.microsoft.com/en-us/library/bb386947(v=vs.110).aspx

            if (data is SqlGeometry)
            {
                return formatGeometry(data, forTSQLScript);
            }
            else if (data is SqlGeography)
            {
                return formatGeography(data, forTSQLScript);
            }
            else if (data is SqlHierarchyId)
            {
                return formatHierarchyId(data);
            }
            else if (data is Guid)
            {
                return formatGuid(data, forTSQLScript);
            }
            else if (data is byte[])
            {
                return formatVarbinary(data);
            }
            else if (data is DateTimeOffset)
            {
                return formatDatetimeoffset(data, forTSQLScript);
            }
            else if (data is DateTime)
            {
                return formatDateTime(data, forTSQLScript);
            }
            else if (data is TimeSpan)
            {
                return formatTime(data, forTSQLScript);
            }
            else if (data is bool)
            {
                return formatBit(data);
            }
            else if (data is decimal || data is Double || data is Single)
            {
                return getDataAsAppropriateNumericFormat(data);
            }
            else if (data is string)
            {
                return "N'" + data.ToString().Replace("'", "''") + "'";
            }

            //All numeric types
            return data.ToString();

        }

        public static string possiblyEncloseInQuotes(string theThing, bool useQuotes)
        {
            if (useQuotes)
            {
                return "'" + theThing + "'";
            }
            return theThing;
        }

        public static string formatGeometry(object data, bool forTSQLScript = true)
        {
            SqlGeometry geom = (SqlGeometry)data;
            return (forTSQLScript ? "geometry::STGeomFromText('" : "")
                    + geom.STAsText().ToSqlString().ToString()
                    + (forTSQLScript ? "'," : " ")
                    + geom.STSrid.ToSqlString().ToString()
                    + (forTSQLScript ? ")" : "");
        }

        public static string formatGeography(object data, bool forTSQLScript = true)
        {
            SqlGeography geog = (SqlGeography)data;
            return (forTSQLScript ? "geography::STGeomFromText('" : "")
                + geog.STAsText().ToSqlString().ToString()
                + (forTSQLScript ? "'," : " ")
                + geog.STSrid.ToSqlString().ToString()
                + (forTSQLScript ? ")" : "");
        }

        public static string formatHierarchyId(object data)
        {
            SqlHierarchyId hier = (SqlHierarchyId)data;
            byte[] ba;
            using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
            using (System.IO.BinaryWriter w = new System.IO.BinaryWriter(ms))
            {
                hier.Write(w);
                w.Flush();
                ba = ms.ToArray();
            }
            return "0x" + BitConverter.ToString(ba).Replace("-", "");
        }

        public static string formatGuid(object data, bool forTSQLScript = true)
        {
            Guid g = (Guid)data;
            return possiblyEncloseInQuotes(g.ToString("D").ToUpper(), forTSQLScript);
        }

        public static string formatImage(object data)
        {
            return formatVarbinary(data);
        }

        public static string formatVarbinary(object data)
        {
            byte[] ba;
            if (data is string)
            {
                return latinHexEncodeStringBytes(data as string);
            }
            else if (data is byte[])
            {
                ba = (byte[])data;
            }
            else
            {
                throw new InvalidCastException("FieldScripting.formatVarbinary can only be called with a string or byte[] argument.");
            }
            
            return "0x" + BitConverter.ToString(ba).Replace("-", "");
        }

        public static string latinHexEncodeStringBytes(string hexEncodedData)
        {
            var bytes = codePage1252Encoding.GetBytes(hexEncodedData);
            return "0x" + BitConverter.ToString(bytes).Replace("-", "");
        }

        public static string formatBit(object data)
        {
            if ((bool)data == true)
            {
                return "1";
            }
            return "0";
        }

        public static string formatSmallDateTime(object data, bool forTSQLScript = true)
        {
            DateTime d = (DateTime)data;
            if (d.Hour == 0 && d.Minute == 0) //smalldatetime doesn't support seconds
            {
                return possiblyEncloseInQuotes(d.ToString("yyyy-MM-dd"), forTSQLScript);
            }
            //but the seconds are required if the time is in ISO string format...
            return possiblyEncloseInQuotes(d.ToString("s"), forTSQLScript);
        }

        public static string formatDateTime(object data, bool forTSQLScript = true)
        {
            string quoting = forTSQLScript ? "'" : "";
            DateTime d = (DateTime)data;
            if (d.ToString("fff") == "000")
            {
                if (d.Hour == 0 && d.Minute == 0 & d.Second == 0)
                {
                    return possiblyEncloseInQuotes(d.ToString("yyyy-MM-dd"), forTSQLScript);
                }
                return possiblyEncloseInQuotes(d.ToString("s"), forTSQLScript);
            }
            return possiblyEncloseInQuotes(String.Format("{0}.{1}",d.ToString("s"), d.ToString("fff").TrimEnd('0')), forTSQLScript);
        }


        public static string formatTime(object data, bool forTSQLScript = true)
        {
            string quoting = forTSQLScript ? "'" : "";
            if (data is TimeSpan)
            {
                TimeSpan t = (TimeSpan)data;
                if (t.Milliseconds == 0)
                {
                    return String.Format("{3}{0}:{1}:{2}{3}",
                        t.Hours.ToString().PadLeft(2, '0'),
                        t.Minutes.ToString().PadLeft(2, '0'),
                        t.Seconds.ToString().PadLeft(2, '0'),
                        quoting);
                }
                return String.Format("{4}{0}:{1}:{2}.{3}{4}",
                        t.Hours.ToString().PadLeft(2, '0'),
                        t.Minutes.ToString().PadLeft(2, '0'),
                        t.Seconds.ToString().PadLeft(2, '0'),
                        t.Milliseconds.ToString().PadLeft(3, '0').TrimEnd('0'),
                        quoting);
            }
            else if (data is DateTime)
            {
                DateTime d = (DateTime)data;

                if (d.ToString("fffffff") == "0000000")
                {
                    return String.Format("{3}{0}:{1}:{2}{3}",
                        d.Hour.ToString().PadLeft(2, '0'),
                        d.Minute.ToString().PadLeft(2, '0'),
                        d.Second.ToString().PadLeft(2, '0'),
                        quoting);
                }
                return String.Format("{4}{0}:{1}:{2}.{3}{4}",
                        d.Hour.ToString().PadLeft(2, '0'),
                        d.Minute.ToString().PadLeft(2, '0'),
                        d.Second.ToString().PadLeft(2, '0'),
                        d.ToString("fffffff").TrimEnd('0'),
                        quoting);
            }

            return String.Format("{1}{0}{1}", data.ToString(), quoting); //todo: this should not get hit, but should have a consistent strategy for dealing with this.
        }

        public static string formatDatetime2(object data, bool forTSQLScript = true)
        {
            DateTime d = (DateTime)data;
            string quoting = forTSQLScript ? "'" : "";

            if (d.ToString("fffffff") == "0000000")
            {
                if (d.Hour == 0 && d.Minute == 0 & d.Second == 0)
                {

                    return String.Format("{1}{0}{1}", d.ToString("yyyy-MM-dd"), quoting);
                }
                return String.Format("{1}{0}{1}", d.ToString("s"), quoting);
                
            }
            return string.Format("{1}{0}.{2}{1}",
                d.ToString("s"),
                quoting,
                d.ToString("fffffff").TrimEnd('0')
                );
        }

        public static string formatDatetimeoffset(object data, bool forTSQLScript = true)
        {
            string quoting = forTSQLScript ? "'" : "";
            DateTimeOffset d = (DateTimeOffset)data;

            if (d.ToString("fffffff") == "0000000")
            {
                return String.Format("{1}{0}{2}{1}",d.ToString("s"),quoting,d.ToString("zzzz"));
            }
            return String.Format("{1}{0}.{3}{2}{1}", d.ToString("s"), quoting, d.ToString("zzzz"), d.ToString("fffffff").TrimEnd('0'));
        }

        public static string formatDate(object data, bool forTSQLScript = true)
        {
            DateTime d = (DateTime)data;
            string quoting = forTSQLScript ? "'" : "";

            return String.Format("{1}{0}{1}",d.ToString("yyyy-MM-dd"),quoting);
        }

        public static string formatTimestamp(object data)
        {
            const int SIZE_OF_TIMESTAMP_IN_BYTES = 8;
            return formatBinary(data, SIZE_OF_TIMESTAMP_IN_BYTES);
        }

        public static string formatBinary(object data, int fieldLength)
        {
            byte[] ba = (byte[])data;
            string bitsAsHexString = BitConverter.ToString(ba).Replace("-", "");
            int charCountToShowAsHex = fieldLength * 2;
            bitsAsHexString = bitsAsHexString.PadLeft(charCountToShowAsHex, '0');
            return "0x" + bitsAsHexString;
        }

        public static string formatNvarchar(object data, bool forTSQLScript = true)
        {
            return String.Format("{1}{0}{2}",
                forTSQLScript ? data.ToString().Replace("'", "''") : data.ToString(),
                singleQuoteIfTrue(forTSQLScript,"N"),
                singleQuoteIfTrue(forTSQLScript));
        }

        public static string formatXml(object data, bool forTSQLScript = true)
        {
            return formatNvarchar(data, forTSQLScript);
        }

        public static string formatNchar(object data, bool forTSQLScript = true)
        {
            return String.Format("{1}{0}{2}",
                forTSQLScript ? data.ToString().Replace("'", "''").TrimEnd() : data.ToString().TrimEnd(),
                singleQuoteIfTrue(forTSQLScript, "N"),
                singleQuoteIfTrue(forTSQLScript));
        }

        public static string formatVarchar(object data, bool forTSQLScript = true)
        {
            return String.Format("{1}{0}{1}",
                forTSQLScript ? data.ToString().Replace("'", "''") : data.ToString(),
                singleQuoteIfTrue(forTSQLScript));
        }

        public static string formatText(object data, bool forTSQLScript = true)
        {
            return formatVarchar(data, forTSQLScript);
        }

        public static string formatNtext(object data, bool forTSQLScript = true)
        {
            return formatNvarchar(data, forTSQLScript);
        }

        public static string formatChar(object data, bool forTSQLScript = true)
        {
            return String.Format("{1}{0}{1}",
                forTSQLScript ? data.ToString().Replace("'", "''").TrimEnd() : data.ToString().TrimEnd(),
                singleQuoteIfTrue(forTSQLScript));
        }

        private static string singleQuoteIfTrue(bool singleQuote, string prependThisString = "")
        {
            return singleQuote ? prependThisString + "'" : "";
        }

        public static Boolean ResultIsRenderableAsCreateTable(FlexResult result)
        {
            return !(result.schema == null || result.schema.Count == 0);
        }

        public static Boolean ResultIsRenderableAsScriptedData(FlexResult result)
        {
            return (result.schema != null && result.data != null && result.data.Count > 0);
        }

        public static StringBuilder ScriptResultDataAsInsert(FlexResult result, string tableName, int maxRowsInValuesClause)
        {
            if (!ResultIsRenderableAsCreateTable(result))
            {
                return new StringBuilder("--No schema for result from query.");
            }

            if (!ResultIsRenderableAsScriptedData(result))
            {
                return new StringBuilder("--No rows were returned from the query.");
            }

            return scriptDataAsInsertForSQL2008Plus(tableName, result, maxRowsInValuesClause);
        }

    }
}

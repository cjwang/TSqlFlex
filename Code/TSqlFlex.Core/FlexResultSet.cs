﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;

namespace TSqlFlex.Core
{
    public class FlexResultSet
    {
        public List<DataTable> schemaTables = null;
        public List<Exception> exceptions = null;
        public List<FlexResult> results = null;
        
        public FlexResultSet() {
            schemaTables = new List<DataTable>();
            exceptions = new List<Exception>();
            results = new List<FlexResult>();        
        }

        public static FlexResultSet AnalyzeResultWithRollback(SqlConnection openConnection, string sqlCommandText) {

            FlexResultSet resultSet = new FlexResultSet();

            if (openConnection.State != System.Data.ConnectionState.Open)
            {
                var emptySqlConn = new ArgumentException("The SqlConnection must be open.");
                resultSet.exceptions.Add(emptySqlConn);
                throw emptySqlConn;
            }
            
            SqlTransaction transaction = openConnection.BeginTransaction("Tran");
            
            try
            {
                SqlCommand cmd = new SqlCommand(sqlCommandText, openConnection, transaction);

                using (SqlDataReader reader = cmd.ExecuteReader(CommandBehavior.KeyInfo))
                {
                    do
                    {
                        //todo: what if first SQL command has an exception - will the second one run/return results?
                        var st = reader.GetSchemaTable();
                        resultSet.schemaTables.Add(st);
                        FlexResult thisResultSet = new FlexResult();
                        int fieldCount = reader.FieldCount;
                        while (reader.Read())
                        {
                            Object[] values = new Object[fieldCount];
                            reader.GetValues(values);
                            thisResultSet.Add(values);
                        }
                        resultSet.results.Add(thisResultSet);

                    } while (reader.NextResult());
                    reader.Close();
                }
            }
            catch (Exception ex)
            {
                resultSet.exceptions.Add(ex);
            }
            finally
            {
                if (transaction != null)
                    transaction.Rollback();
            }
            return resultSet;
        }

        //todo: columnnames must be unique in a table.  It's possible to have a result set with duplicate column names, but not a table.

        public string ScriptResultAsCreateTable(int resultIndex, string tableName)
        {
            if (schemaTables[resultIndex] == null)
            {
                return "--No schema for result from query.";
            }
            var rows = schemaTables[resultIndex].Rows;
            StringBuilder buffer = new StringBuilder("CREATE TABLE " + tableName + "(\r\n");
            for (int fieldIndex = 0; fieldIndex < rows.Count; fieldIndex++)
            {
                var fieldInfo = rows[fieldIndex];
                buffer.Append("    " +
                        FieldNameOrDefault(fieldInfo, fieldIndex) +
                        " " +
                        DataType(fieldInfo) +
                        DataTypeParameterIfAny(fieldInfo) + 
                        " " +
                        NullOrNotNull(fieldInfo[13])
                        );
                if (fieldIndex + 1 < rows.Count)
                {
                    buffer.Append(",\r\n");
                } else {
                    buffer.Append("\r\n");
                }
            }
            buffer.Append(");\r\n");
            return buffer.ToString();
        }

        private string FieldNameOrDefault(DataRow fieldInfo, int fieldIndex)
        {
            var r = fieldInfo[0].ToString();
            if (r.Length == 0)
            {
                return "anonymousColumn" + (fieldIndex + 1).ToString();
            }
            return r;
        }

        private string DataType(DataRow fieldInfo)
        {
            var fieldName = fieldInfo[24].ToString();
            if (fieldName == "real")
            {
                return "float";  //this could be a float or a real.  There is no simple way to tell via ado.net.  Will try to keep it consistent with float.
            }
            else if (fieldName.EndsWith(".sys.hierarchyid"))
            {
                return "hierarchyid";
            }
            else if (fieldName.EndsWith(".sys.geography"))
            {
                return "geography";
            }
            else if (fieldName.EndsWith(".sys.geometry"))
            {
                return "geometry"; 
            }
            return fieldName;
        }

        private string DataTypeParameterIfAny(DataRow fieldInfo)
        {
            var dataTypeName = fieldInfo[24].ToString();
            if (dataTypeName == "nvarchar" || dataTypeName == "varchar" || dataTypeName == "nchar" || dataTypeName == "char" || dataTypeName == "binary" || dataTypeName == "varbinary")
            {
                int columnSize = (int)fieldInfo[2];    
                if (columnSize == Int32.MaxValue)
                {
                    return "(MAX)";
                }
                return "(" + columnSize.ToString() + ")";
            }
            else if (dataTypeName == "numeric" || dataTypeName == "decimal")
            {
                int numericPrecision = (short)fieldInfo[3];
                int numericScale = (short)fieldInfo[4];
                return "(" + numericPrecision.ToString() + "," + numericScale.ToString() + ")";
            }
            else if (dataTypeName == "real")
            {
                return "(24)";
            }
            else if (dataTypeName == "float")
            {
                //from MSDN: SQL Server treats n as one of two possible values. If 1<=n<=24, n is treated as 24. If 25<=n<=53, n is treated as 53.
                return "(53)";
            }
            return "";
        }

        public string NullOrNotNull(Object allowDbNull)
        {
            bool allowDBNullFlag;
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
    }
}

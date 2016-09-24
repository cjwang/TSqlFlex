﻿using System.Collections.Generic;
using NUnit.Framework;

namespace TSqlFlex.Core.Tests
{
    [TestFixture()]
    public class FlexResultSetTests
    {
        [Test()]
        public void CreatingEmptyFlexResultSet_ResultsInEmptyCollections()
        {
            FlexResultSet fsr = new FlexResultSet();
            Assert.IsNotNull(fsr);

            Assert.IsNotNull(fsr.results);
            Assert.AreEqual(0, fsr.results.Count);
        }

        [Test()]
        public void ResultSet_WithNoReturnedSchema_ResultsInNoReturnedSchemaComment()
        {
            FlexResultSet fsr = new FlexResultSet();

            var result = new FlexResult();

            fsr.results.Add(result);

            Assert.AreEqual("--No schema for result from query.", FieldScripting.ScriptResultDataAsInsert(result, "#result0", FlexResultSet.SQL2008MaxRowsInValuesClause).ToString());
        }

        [Test()]
        public void ResultSet_WithNoReturnedData_ResultsInNoReturnedDataComment()
        {
            FlexResultSet fsr = new FlexResultSet();

            var dt = new List<SQLColumn>() { 
                SchemaScriptingTests.FakeColumn("IntNotNull", "MyStuff", 32, "int", false, 255, 255),
                SchemaScriptingTests.FakeColumn("IntNull", "MyStuff", 32, "int", true, 255, 255)
            };

            FlexResult result = new FlexResult();

            fsr.results.Add(result);
            fsr.results[0].schema = dt;
            fsr.results[0].data = new List<object[]>();

            Assert.AreEqual("--No rows were returned from the query.", FieldScripting.ScriptResultDataAsInsert(result, "#result0", FlexResultSet.SQL2008MaxRowsInValuesClause).ToString());
        }
    }
}

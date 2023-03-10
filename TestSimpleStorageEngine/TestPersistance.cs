using System;
using System.Text;
using System.Collections.Generic;
using NUnit.Framework;
using SimpleStorageEngine.Persistance.ExtensibleStorageEngine;
using SimpleStorageEngine.Persistance;
using System.IO;
using System.Linq;

namespace TestSimpleStorageEngine {

    [TestFixture]
    public class TestPersistance {
        public TestPersistance() {
        }


        #region Additional test attributes
       
        static string directory = "test_data";
        static string filename = Path.GetFullPath(directory + "\\test.edb");

        ConnectionManager connectionManager;

        [SetUp]
        public void MyTestInitialize() 
        {
            Directory.CreateDirectory(directory);

            connectionManager = new EseConnectionManager(filename);
            connectionManager.CreateDatabase(); 
        }
        
        [TearDown]
        public void MyTestCleanup() 
        {
            Directory.Delete(directory, true);
        }
        
        #endregion

        private Connection GetConnection() {
            return connectionManager.GetConnection();
        }

        [Test]
        public void TestTableCreation() 
        {
            CreatePersonTable();

            using (var connection = GetConnection()) {
                Table table = connection.GetTable("person");
                var columns = table.Columns;
                columns.Sort((x,y) => x.Name.CompareTo(y.Name)); 
                Assert.AreEqual(2, columns.Count);
                Assert.AreEqual("name", table.Columns[0].Name);
                Assert.AreEqual(false, table.Columns[0].IsPrimaryKey); 
                Assert.AreEqual("ssn", table.Columns[1].Name);
                Assert.AreEqual(true, table.Columns[1].IsPrimaryKey); 
            }

        }

        private void CreatePersonTable() {
            using (var connection = GetConnection()) {
                connection.CreateTable(new TableDefinition("person")
                    .AddColumn("ssn", typeof(int), ColumnProperties.PrimaryKey)
                    .AddColumn("name", typeof(string))
                    );
            }
        }

        [Test]
        public void TestDataLookup() 
        {
            CreatePersonTable();

            using (var connection = GetConnection()) 
            {
                var t = connection.GetTable("person");
                var row = new Row();
                row["ssn"] = 1000;
                row["name"] = "Booboo";
                t.Insert(row); 
            }

            using (var connection = GetConnection()) {
                var t = connection.GetTable("person");
                var row = t.GetRow(1000);
                Assert.AreEqual(row["ssn"], 1000);
                Assert.AreEqual(row["name"], "Booboo"); 
            }
        }

        [Test] 
        public void TestDataDeletion() 
        {
            CreatePersonTable();
            using (var connection = GetConnection()) 
            {
                var t = connection.GetTable("person");
                var row = new Row();
                row["ssn"] = 1000;
                row["name"] = "Booboo";
                t.Insert(row); 
            }

            using (var connection = GetConnection()) 
            {
                var t = connection.GetTable("person");
                Assert.AreEqual(true, t.Exists(1000)); 
                t.Delete(1000);
                Assert.AreEqual(false, t.Exists(1000)); 
            }
        }

        [Test]
        public void TestTruncate() 
        {
            CreatePersonTable();
            using (var connection = GetConnection()) 
            {
                var t = connection.GetTable("person");
                
                for (int i = 0; i < 100; i++) {
                    var row = new Row();
                    row["ssn"] = i;
                    row["name"] = "Booboo";
                    t.Insert(row); 
                }
                Assert.AreEqual(100, t.Count); 
                t.Truncate();
                Assert.AreEqual(0, t.Count);
            }
        }

        [Test]
        public void TestGetAllRows() {
            CreatePersonTable();
            using (var connection = GetConnection()) {
                var t = connection.GetTable("person");
                for (int i = 0; i < 100; i++) {
                    var row = new Row();
                    row["ssn"] = i;
                    row["name"] = "Booboo" + i.ToString();
                    t.Insert(row);
                }
                var rows = t.GetRows();
                Assert.AreEqual(100,Enumerable.Count(rows)); 
            } 
        }

        [Test]
        public void TestAutoIncrement() {
            using (var connection = GetConnection()) {
                connection.CreateTable(new TableDefinition("person")
                    .AddColumn("ssn", typeof(int), ColumnProperties.PrimaryKey | ColumnProperties.AutoIncrement)
                    .AddColumn("name", typeof(string))
                    );
            }

            using (var connection = GetConnection()) {
                var t = connection.GetTable("person");
                var row = new Row().SetValue("name", "bob");
                var row2 = new Row().SetValue("name", "bob2"); 
                t.Insert(row);
                t.Insert(row2);

                Assert.AreEqual(t.Count, 2);

                Assert.AreEqual(row, t.GetRow(row["ssn"]));
                Assert.AreEqual(row2, t.GetRow(row2["ssn"])); 
            }
            
        }

        [Test]
        public void TestPartialLookup() {
            using (var connection = GetConnection()) {
                connection.CreateTable(new TableDefinition("person")
                    .AddColumn("id", typeof(int), ColumnProperties.PrimaryKey)
                    .AddColumn("firstname", typeof(string))
                    .AddColumn("lastname", typeof(string))
                    .AddIndex("lastname")
                    );

                var t = connection.GetTable("person"); 

                for (int i = 0; i < 100; i++) {
                    t.Insert(new Row()
                        .SetValue("id",i)
                        .SetValue("firstname", "aaa" + i.ToString())
                        .SetValue("lastname", "bbb")); 
                }

                for (int i = 100; i < 150; i++) {
                    t.Insert(new Row()
                        .SetValue("id", i)
                        .SetValue("firstname", "aaa" + i.ToString())
                        .SetValue("lastname", "ccc"));
                }

                var rows = t.GetRows(new Row().SetValue("lastname", "ccc"));
                Assert.AreEqual(50, Enumerable.Count(rows)); 
            }
        }

        [Test]
        public void TestTransaction() {
            CreatePersonTable();
            using (var connection = GetConnection()) {
                var table = connection.GetTable("person");
                
                using (var transaction = connection.BeginTransaction()) {
                    table.Insert(new Row()
                        .SetValue("ssn", 1)
                        .SetValue("name", "bob bob"));    
                    transaction.Commit();
                }
                
                using (var transaction = connection.BeginTransaction()) {
                    table.Insert(new Row()
                       .SetValue("ssn", 2)
                       .SetValue("name", "bill bill"));
                    transaction.Rollback();
                }

                Assert.AreEqual(1, table.Count);
            }
        }

        [Test]
        public void TestUpsert() 
        {
            CreatePersonTable(); 
            using (var connection = GetConnection()) {

                var t = connection.GetTable("person");

                var row = new Row();
                row["ssn"] = 1;
                row["name"] = "Booboo";
                t.Upsert(row);

                Assert.AreEqual("Booboo", t.GetRow(1)["name"]);

                row["name"] = "bob";
                t.Upsert(row); 
                
                Assert.AreEqual("bob", t.GetRow(1)["name"]);
            }
        }

        [Test]
        public void TestAddingAColumnToATable() {
            CreatePersonTable();

            using (var connection = GetConnection()) {
                var t = connection.GetTable("person"); 
                t.AddColumn(new ColumnDefinition("comments", typeof(string)));
                t.Insert(new Row().SetValue("ssn", 1)
                    .SetValue("name", "test")
                    .SetValue("comments", "here go the comments")
                    );

                Assert.AreEqual("here go the comments", t.GetRow(1)["comments"]); 
            }
        }

    }
}

// -----------------------------------------------------------------------
//  <copyright file="RavenDB_302.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------
using System;
using System.Runtime.Serialization;

using Raven.Abstractions.Data;
using Raven.Abstractions.Indexing;
using Raven.Client.Document;
using Raven.Client.Indexes;

using Xunit;
using System.Linq;
using FastTests;
using Raven.Client.Data;
using Raven.Client.Indexing;

namespace SlowTests.Issues
{
    public class RavenDB_302 : RavenTestBase
    {
        [DataContract]
        public class Item
        {
            [DataMember]
            public string Version { get; set; }
        }

        [Fact]
        public void CanQueryUsingDefaultField()
        {
            using(var s = GetDocumentStore())
            {
                using (var session = s.OpenSession())
                {
                    session.Store(new Item{Version = "first"});
                    session.Store(new Item { Version = "second" });
                    session.SaveChanges();
                }
                using(var session = s.OpenSession())
                {
                    var x = session.Advanced.DocumentQuery<Item>()
                        .WaitForNonStaleResults()
                        .UsingDefaultField("Version")
                        .Where("First OR Second")
                        .ToList();

                    Assert.Equal(2, x.Count);
                }
            }
        }

        public class Node
        {
            public string FirstName { get; set; }
            public string LastName { get; set; }
        }

        public class Index : AbstractIndexCreationTask<Node>
        {
            public Index()
            {
                Map = nodes =>
                      from node in nodes
                      select new
                      {
                          node.LastName,
                          Query = new[]{node.FirstName, node.LastName}
                      };
            }
        }

        [Fact]
        public void CanQueryUsingDefaultField_StaticIndex()
        {
            using (var s = GetDocumentStore())
            {
                new Index().Execute(s);

                using (var session = s.OpenSession())
                {
                    session.Store(new Node { FirstName= "jonas", LastName = "brown"});
                    session.Store(new Node { FirstName = "arik", LastName = "smith"});
                    session.SaveChanges();
                }
                using (var session = s.OpenSession())
                {
                    var x = session.Advanced.DocumentQuery<Node, Index>()
                        .WaitForNonStaleResults()
                        .UsingDefaultField("Query")
                        .Where("jonas OR smith")
                        .ToList();

                    Assert.Equal(2, x.Count);
                }
            }
        }

        [Fact]
        public void CanQueryUsingDefaultField_Facets()
        {
            using (var s = GetDocumentStore())
            {
                new Index().Execute(s);

                using (var session = s.OpenSession())
                {
                    session.Store(new Node { FirstName = "jonas", LastName = "brown" });
                    session.Store(new Node { FirstName = "arik", LastName = "smith" });
                    session.Store(new FacetSetup
                    {
                        Id = "Raven/Facets/LastName",
                        Facets =
                        {
                            new Facet
                            {
                                Mode = FacetMode.Default,
                                Name = "LastName"
                            }
                        }
                    });
                    session.SaveChanges();
                }
                using (var session = s.OpenSession())
                {
                    var x = session.Advanced.DocumentQuery<Node, Index>()
                        .WaitForNonStaleResults()
                        .UsingDefaultField("Query")
                        .Where("jonas");

                    GC.KeepAlive(x.ToList());// wait for the index to complete

                    var indexQuery = new IndexQuery {Query = x.ToString(), DefaultField = "Query"};
                    var facet = FacetQuery.Create("Index", indexQuery,"Raven/Facets/LastName",null,0,null);

                    var ravenfacets = s.DatabaseCommands.GetFacets(facet);

                    Assert.Equal(1, ravenfacets.Results["LastName"].Values.First(y=>y.Range == "brown").Hits);
                }
            }
        }

        [Fact]
        public void CanQueryUsingDefaultField_Remote()
        {
            using (var s = GetDocumentStore())
            {
                s.DatabaseCommands.PutIndex("items_by_ver", new IndexDefinition
                {
                    Maps = {"from doc in docs.Items select new { doc.Version }" }
                });
                using (var session = s.OpenSession())
                {
                    session.Store(new Item { Version = "first" });
                    session.Store(new Item { Version = "second" });
                    session.SaveChanges();
                }
                using (var session = s.OpenSession())
                {
                    var x = session.Advanced.DocumentQuery<Item>("items_by_ver")
                        .WaitForNonStaleResults()
                        .UsingDefaultField("Version")
                        .Where("First OR Second")
                        .ToList();

                    Assert.Equal(2, x.Count);
                }
            }
        }
    }
}
﻿// -----------------------------------------------------------------------
//  <copyright file="SmugglerBetweenEmbeddedTests_RavenDB_3318.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading.Tasks;

using Raven.Abstractions.Data;
using Raven.Abstractions.Database.Smuggler;
using Raven.Abstractions.Database.Smuggler.Database;
using Raven.Client;
using Raven.Client.Document;
using Raven.Database.Smuggler.Embedded;
using Raven.Smuggler.Database;
using Raven.Smuggler.Database.Remote;
using Raven.Tests.Common;

using Xunit;

namespace Raven.Tests.Smuggler
{
	public class SmugglerBetweenFromEmbeddedTests_RavenDB_3318 : RavenTest
	{
		[Fact]
		public async Task BasicBetweenTestFromEmbeddedStore()
		{
			using (var store = NewDocumentStore())
			{
				await new SmugglerBetweenTests.UsersIndex().ExecuteAsync(store.AsyncDatabaseCommands, new DocumentConvention());
				await new SmugglerBetweenTests.UsersTransformer().ExecuteAsync(store);
				using (var session = store.OpenAsyncSession())
				{
					await session.StoreAsync(new SmugglerBetweenTests.User { Name = "Robert" });
					await session.StoreAsync(new SmugglerBetweenTests.User { Name = "James" });
					await session.SaveChangesAsync();
				}

				using (var server = GetNewServer(port: 8078))
				{
					using (var targetStore = NewRemoteDocumentStore(ravenDbServer: server, databaseName: "TargetDB"))
					{
					    var smuggler = new DatabaseSmuggler(
                            new DatabaseSmugglerOptions(), 
                            new DatabaseSmugglerEmbeddedSource(store.DocumentDatabase), 
                            new DatabaseSmugglerRemoteDestination(new DatabaseSmugglerRemoteConnectionOptions
                            {
                                Url = "http://localhost:8078",
                                Database = "TargetDB"
                            }));

					    await smuggler.ExecuteAsync();

						await SmugglerBetweenTests.AssertDatabaseHasIndex<SmugglerBetweenTests.UsersIndex>(targetStore);
						await SmugglerBetweenTests.AssertDatabaseHasTransformer<SmugglerBetweenTests.UsersTransformer>(targetStore);

						using (var session = targetStore.OpenAsyncSession())
						{
							Assert.NotNull(await session.LoadAsync<SmugglerBetweenTests.User>("users/1"));
							Assert.NotNull(await session.LoadAsync<SmugglerBetweenTests.User>("users/2"));

							var users = await session.Query<SmugglerBetweenTests.User, SmugglerBetweenTests.UsersIndex>().Customize(x => x.WaitForNonStaleResults()).ToListAsync();

							Assert.Equal(2, users.Count);
						}
					}
				}
			}
		}

		[Fact]
		public async Task ShouldSupportIncremental()
		{
            throw new NotImplementedException();

			using (var store = NewDocumentStore())
			{
				await new SmugglerBetweenTests.UsersIndex().ExecuteAsync(store.AsyncDatabaseCommands, new DocumentConvention());
				await new SmugglerBetweenTests.UsersTransformer().ExecuteAsync(store);
				using (var session = store.OpenAsyncSession())
				{
					await session.StoreAsync(new SmugglerBetweenTests.User { Name = "Robert" }, "users/1");
					await session.StoreAsync(new SmugglerBetweenTests.User { Name = "James" }, "users/2");
					await session.SaveChangesAsync();
				}

				using (var server = GetNewServer(port: 8078))
				{
					using (var targetStore = NewRemoteDocumentStore(ravenDbServer: server, databaseName: "TargetDB"))
					{
						//var smuggler = new DatabaseDataDumper(store.DocumentDatabase, new SmugglerDatabaseOptions()
						//{
						//	Incremental = true
						//});

						//await smuggler.Between(new SmugglerBetweenOptions<RavenConnectionStringOptions>
						//{
						//	To = new RavenConnectionStringOptions
						//	{
						//		Url = "http://localhost:8078",
						//		DefaultDatabase = "TargetDB"
						//	}
						//});

						await SmugglerBetweenTests.AssertDatabaseHasIndex<SmugglerBetweenTests.UsersIndex>(targetStore);
						await SmugglerBetweenTests.AssertDatabaseHasTransformer<SmugglerBetweenTests.UsersTransformer>(targetStore);

						using (var session = store.OpenAsyncSession())
						{
							var oren = await session.LoadAsync<SmugglerBetweenTests.User>("users/1");
							oren.Name += " Smith";
							await session.StoreAsync(new SmugglerBetweenTests.User { Name = "David" }, "users/3");
							await session.SaveChangesAsync();
						}

						//await smuggler.Between(new SmugglerBetweenOptions<RavenConnectionStringOptions>
						//{
						//	To = new RavenConnectionStringOptions
						//	{
						//		Url = "http://localhost:8078",
						//		DefaultDatabase = "TargetDB"
						//	}
						//});

						using (var session = targetStore.OpenAsyncSession())
						{
							var changedUser = await session.LoadAsync<SmugglerBetweenTests.User>("users/1");
							Assert.NotNull(changedUser);

							Assert.Equal("Robert Smith", changedUser.Name);

							Assert.NotNull(await session.LoadAsync<SmugglerBetweenTests.User>("users/2"));
							Assert.NotNull(await session.LoadAsync<SmugglerBetweenTests.User>("users/3"));

							var users = await session.Query<SmugglerBetweenTests.User, SmugglerBetweenTests.UsersIndex>().Customize(x => x.WaitForNonStaleResults()).ToListAsync();

							Assert.Equal(3, users.Count);
						}
					}
				}
			}
		}
	}
}
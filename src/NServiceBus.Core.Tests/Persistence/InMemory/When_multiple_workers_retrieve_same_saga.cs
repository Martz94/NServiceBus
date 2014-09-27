﻿namespace NServiceBus.SagaPersisters.InMemory.Tests
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.Features;
    using NServiceBus.InMemory.SagaPersister;
    using NUnit.Framework;

    [TestFixture]
    class When_multiple_workers_retrieve_same_saga
    {
        InMemorySagaPersister inMemorySagaPersister = new InMemorySagaPersister(TypeBasedSagaMetaModel.Create<TestSaga>());
     
        [Test]
        public void Persister_returns_different_instance_of_saga_data()
        {
            var saga = new TestSagaData { Id = Guid.NewGuid() };
            inMemorySagaPersister.Save(saga);

            var returnedSaga1 = inMemorySagaPersister.Get<TestSagaData>(saga.Id);
            var returnedSaga2 = inMemorySagaPersister.Get<TestSagaData>("Id", saga.Id);
            Assert.AreNotSame(returnedSaga2, returnedSaga1);
            Assert.AreNotSame(returnedSaga1, saga);
            Assert.AreNotSame(returnedSaga2, saga);
        }

        [Test]
        public void Save_fails_when_data_changes_between_read_and_update()
        {
            var saga = new TestSagaData { Id = Guid.NewGuid() };
            inMemorySagaPersister.Save(saga);

            var returnedSaga1 = Task<TestSagaData>.Factory.StartNew(() => inMemorySagaPersister.Get<TestSagaData>(saga.Id)).Result;
            var returnedSaga2 = inMemorySagaPersister.Get<TestSagaData>("Id", saga.Id);

            inMemorySagaPersister.Save(returnedSaga1);
            var exception = Assert.Throws<Exception>(() => inMemorySagaPersister.Save(returnedSaga2));
            Assert.IsTrue(exception.Message.StartsWith(string.Format("InMemorySagaPersister concurrency violation: saga entity Id[{0}] already saved by [Worker.", saga.Id)));
        }

        [Test]
        public void Save_process_is_repeatable()
        {
            var saga = new TestSagaData { Id = Guid.NewGuid() };
            inMemorySagaPersister.Save(saga);

            var returnedSaga1 = Task<TestSagaData>.Factory.StartNew(() => inMemorySagaPersister.Get<TestSagaData>(saga.Id)).Result;
            var returnedSaga2 = inMemorySagaPersister.Get<TestSagaData>("Id", saga.Id);

            inMemorySagaPersister.Save(returnedSaga1);
            var exceptionFromSaga2 = Assert.Throws<Exception>(() => inMemorySagaPersister.Save(returnedSaga2));
            Assert.IsTrue(exceptionFromSaga2.Message.StartsWith(string.Format("InMemorySagaPersister concurrency violation: saga entity Id[{0}] already saved by [Worker.", saga.Id)));

            var returnedSaga3 = Task<TestSagaData>.Factory.StartNew(() => inMemorySagaPersister.Get<TestSagaData>("Id", saga.Id)).Result;
            var returnedSaga4 = inMemorySagaPersister.Get<TestSagaData>(saga.Id);

            inMemorySagaPersister.Save(returnedSaga4);

            var exceptionFromSaga3 = Assert.Throws<Exception>(() => inMemorySagaPersister.Save(returnedSaga3));
            Assert.IsTrue(exceptionFromSaga3.Message.StartsWith(string.Format("InMemorySagaPersister concurrency violation: saga entity Id[{0}] already saved by [Worker.", saga.Id)));
        }
    }
}

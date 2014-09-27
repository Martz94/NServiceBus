﻿namespace NServiceBus.SagaPersisters.InMemory.Tests
{
    using System;
    using NServiceBus.Features;
    using NServiceBus.InMemory.SagaPersister;
    using NUnit.Framework;

    [TestFixture]
    public class When_completing_a_saga_with_the_InMemory_persister
    {
        [Test]
        public void Should_delete_the_saga()
        {
            var inMemorySagaPersister = new InMemorySagaPersister(TypeBasedSagaMetaModel.Create<TestSaga>());
            var saga = new TestSagaData { Id = Guid.NewGuid() };
            
            inMemorySagaPersister.Save(saga);
            Assert.NotNull(inMemorySagaPersister.Get<TestSagaData>(saga.Id));
            inMemorySagaPersister.Complete(saga);
            Assert.Null(inMemorySagaPersister.Get<TestSagaData>(saga.Id));
        }
    }
}

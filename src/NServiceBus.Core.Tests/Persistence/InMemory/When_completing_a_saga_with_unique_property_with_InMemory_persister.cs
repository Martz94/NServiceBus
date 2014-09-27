namespace NServiceBus.SagaPersisters.InMemory.Tests
{
    using System;
    using NServiceBus.Features;
    using NServiceBus.InMemory.SagaPersister;
    using NUnit.Framework;
    
    [TestFixture]
    class When_completing_a_saga_with_unique_property_with_InMemory_persister
    {
        [Test]
        public void Should_delete_the_saga()
        {
            var inMemorySagaPersister = new InMemorySagaPersister(TypeBasedSagaMetaModel.Create<SagaWithUniqueProperty>());
            var saga = new SagaWithUniquePropertyData { Id = Guid.NewGuid(), UniqueString = "whatever" };

            inMemorySagaPersister.Save(saga);
            Assert.NotNull(inMemorySagaPersister.Get<SagaWithUniquePropertyData>(saga.Id));
            inMemorySagaPersister.Complete(saga);
            Assert.Null(inMemorySagaPersister.Get<SagaWithUniquePropertyData>(saga.Id));
        }
    }
}

namespace NServiceBus.Features
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NServiceBus.Saga;

    /// <summary>
    ///     Used to configure saga.
    /// </summary>
    public class Sagas : Feature
    {
        internal Sagas()
        {
            EnableByDefault();

            Defaults(s =>
            {
                conventions = s.Get<Conventions>();

                var sagas = s.GetAvailableTypes().Where(IsSagaType).ToList();
                if (sagas.Count > 0)
                {
                    conventions.AddSystemMessagesConventions(t => IsTypeATimeoutHandledByAnySaga(t, sagas));
                }
            });

            Prerequisite(config => config.Settings.GetAvailableTypes().Any(IsSagaType), "No sagas was found in scabbed types");
        }

        /// <summary>
        ///     See <see cref="Feature.Setup" />
        /// </summary>
        protected internal override void Setup(FeatureConfigurationContext context)
        {
            // Register the Saga related behavior for incoming messages
            context.Pipeline.Register<AssociateMessageWithSagaBehavior.Registration>();
            context.Pipeline.Register<SagaPersistenceBehavior.Registration>();

      
            var sagaMetaModel = TypeBasedSagaMetaModel.Create(context.Settings.GetAvailableTypes(),conventions);

            foreach (var finder in sagaMetaModel.All.SelectMany(m=>m.Finders))
            {
                context.Container.ConfigureComponent(finder.Type, DependencyLifecycle.InstancePerCall);

                //todo improve

                object customFinderType;

                if (finder.Properties.TryGetValue("custom-finder-clr-type", out customFinderType))
                {
                    context.Container.ConfigureComponent((Type)customFinderType, DependencyLifecycle.InstancePerCall);

                }

            }

            context.Container.RegisterSingleton(sagaMetaModel);

            foreach (var t in context.Settings.GetAvailableTypes())
            {
                if (IsSagaNotFoundHandler(t))
                {
                    context.Container.ConfigureComponent(t, DependencyLifecycle.InstancePerCall);
                }
            }

        }


        static bool IsSagaType(Type t)
        {
            return IsCompatible(t, typeof(Saga));
        }

        
        static bool IsSagaNotFoundHandler(Type t)
        {
            return IsCompatible(t, typeof(IHandleSagaNotFound));
        }

        static bool IsCompatible(Type t, Type source)
        {
            return source.IsAssignableFrom(t) && t != source && !t.IsAbstract && !t.IsInterface && !t.IsGenericType;
        }

        static bool IsTypeATimeoutHandledByAnySaga(Type type, IEnumerable<Type> sagas)
        {
            var timeoutHandler = typeof(IHandleTimeouts<>).MakeGenericType(type);
            var messageHandler = typeof(IHandleMessages<>).MakeGenericType(type);

            return sagas.Any(t => timeoutHandler.IsAssignableFrom(t) && !messageHandler.IsAssignableFrom(t));
        }

        internal static IEnumerable<Type> GetMessageTypesHandledBySaga(Type sagaType, Conventions conventions)
        {
            return GetMessagesCorrespondingToFilterOnSaga(sagaType, typeof(IHandleMessages<>), conventions);
        }

        internal static IEnumerable<Type> GetMessageTypesThatRequireStartingTheSaga(Type sagaType, Conventions conventions)
        {
            return GetMessagesCorrespondingToFilterOnSaga(sagaType, typeof(IAmStartedByMessages<>), conventions);
        }

        static IEnumerable<Type> GetMessagesCorrespondingToFilterOnSaga(Type sagaType, Type filter, Conventions conventions)
        {
            foreach (var interfaceType in sagaType.GetInterfaces())
            {
                foreach (var argument in interfaceType.GetGenericArguments())
                {
                    var genericType = filter.MakeGenericType(argument);
                    var isOfFilterType = genericType == interfaceType;
                    if (!isOfFilterType)
                    {
                        continue;
                    }
                    if (conventions.IsMessageType(argument))
                    {
                        yield return argument;
                        continue;
                    }
                    var message = string.Format("The saga '{0}' implements '{1}' but the message type '{2}' is not classified as a message. You should either use 'Unobtrusive Mode Messages' or the message should implement either 'IMessage', 'IEvent' or 'ICommand'.", sagaType.FullName, genericType.Name, argument.FullName);
                    throw new Exception(message);
                }
            }
        }

        Conventions conventions;
    }
}
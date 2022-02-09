using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PactNet.Exceptions;
using PactNet.Interop;
using PactNet.Models;

namespace PactNet
{
    /// <summary>
    /// Mock request builder
    /// </summary>
    public class MessagePactBuilder : IMessagePactBuilderV3
    {
        private readonly IMessageMockServer server;
        private readonly MessagePactHandle pact;
        private readonly PactConfig config;
        private MessageHandle message;

        /// <summary>
        /// Initialises a new instance of the <see cref="MessagePactBuilder"/> class.
        /// </summary>
        /// <param name="server">Mock server</param>
        /// <param name="pact">the message pact handle</param>
        /// <param name="config">the message pact configuration</param>
        internal MessagePactBuilder(IMessageMockServer server, MessagePactHandle pact, PactConfig config)
        {
            this.pact = pact;
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.server = server ?? throw new ArgumentNullException(nameof(server));
        }

        #region IMessagePactBuilderV3 explicit implementation

        /// <inheritdoc cref="IMessagePactBuilderV3"/>
        IMessageBuilderV3 IMessagePactBuilderV3.ExpectsToReceive(string description)
            => ExpectsToReceive(description);

        /// <inheritdoc cref="IMessagePactBuilderV3"/>
        IMessagePactBuilderV3 IMessagePactBuilderV3.WithPactMetadata(string @namespace, string name, string value)
            => WithPactMetadata(@namespace, name, value);

        #endregion

        #region Internal Methods

        /// <summary>
        /// Add a new message to the message pact
        /// </summary>
        /// <param name="description">Message description</param>
        /// <returns>Fluent builder</returns>
        internal IMessageBuilderV3 ExpectsToReceive(string description)
        {
            this.message = this.server.NewMessage(this.pact, description);

            this.server.ExpectsToReceive(this.message, description);

            return new MessageBuilder(this.server, this.message, this.config.DefaultJsonSettings);
        }

        /// <summary>
        /// Add a new metadata to the message pact
        /// </summary>
        /// <param name="namespace">the parent configuration section</param>
        /// <param name="name">the metadata field value</param>
        /// <param name="value">the metadata field value</param>
        /// <returns>Fluent builder</returns>
        internal IMessagePactBuilderV3 WithPactMetadata(string @namespace, string name, string value)
        {
            this.server.WithMessagePactMetadata(this.pact, @namespace, name, value);

            return this;
        }

        /// <summary>
        /// Verify a message is read and handled correctly and write the message pact
        /// </summary>
        /// <param name="handler">The method using the message</param>
        public void Verify<T>(Action<T> handler)
        {
            try
            {
                var messageReified = MessageReified<T>();

                handler(messageReified);

                WritePact();
            }
            catch (Exception e)
            {
                throw new PactMessageConsumerVerificationException(
                    $"The message {this.message} could not be verified by the consumer handler", e);
            }
        }

        /// <summary>
        /// Verify a message is read and handled correctly and write the message pact
        /// </summary>
        /// <param name="handler">The method using the message</param>
        public async Task VerifyAsync<T>(Func<T, Task> handler)
        {
            try
            {
                var messageReified = MessageReified<T>();

                await handler(messageReified);

                WritePact();
            }
            catch (Exception e)
            {
                throw new PactMessageConsumerVerificationException($"The message {this.message} could not be verified by the consumer handler", e);
            }
        }

        #endregion Internal Methods

        #region Private Methods

        /// <summary>
        /// Try to read the reified message
        /// </summary>
        /// <typeparam name="T">the type of message</typeparam>
        /// <returns>the message</returns>
        private T MessageReified<T>()
        {
            string reified = this.server.Reify(this.message);
            NativeMessage content = JsonConvert.DeserializeObject<NativeMessage>(reified);

            T messageReified = JsonConvert.DeserializeObject<T>(content.Contents.ToString());
            return messageReified;
        }

        /// <summary>
        /// Write the pact file
        /// </summary>
        private void WritePact()
        {
            this.server.WriteMessagePactFile(this.pact, this.config.PactDir, false);
        }

        #endregion Private Methods
    }
}
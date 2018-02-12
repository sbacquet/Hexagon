using System;
using System.Threading.Tasks;
using Akka.Cluster.Tools.PublishSubscribe;
using Akka.Actor;

namespace Hexagon.AkkaImpl
{
    public class ActorPathMessageReceiver<M> : ICanReceiveMessage<M>
        where M : IMessage
    {
        public readonly string ActorPath;
        IActorRef Mediator { get; }
        public ActorPathMessageReceiver(string actorPath, IActorRef mediator)
        {
            ActorPath = actorPath;
            Mediator = mediator;
        }
        public void Tell(M message, ICanReceiveMessage<M> sender)
        {
            if (sender != null)
                Mediator.Tell(new Send(ActorPath, new BytesMessage(message.Bytes)), (sender as ActorRefMessageReceiver<M>).Actor);
            else
                Mediator.Tell(new Send(ActorPath, new BytesMessage(message.Bytes)));
        }
        public async Task<M> Ask(M message, IMessageFactory<M> factory, TimeSpan? timeout = null, System.Threading.CancellationToken? cancellationToken = null)
        {
            var bytesMessageTask = 
                cancellationToken.HasValue ?
                    Mediator.Ask<BytesMessage>(new Send(ActorPath, new BytesMessage(message.Bytes)), timeout, cancellationToken.Value)
                :
                    Mediator.Ask<BytesMessage>(new Send(ActorPath, new BytesMessage(message.Bytes)), timeout);
            return await bytesMessageTask.ContinueWith<M>(task => factory.FromBytes(task.Result.Bytes));
        }

        public string Path => ActorPath;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.XPath;

namespace Hexagon
{
    [Serializable]
    public class BytesMessage
    {
        public readonly byte[] Bytes;
        public BytesMessage(byte[] bytes)
        {
            Bytes = bytes;
        }
    }
    public interface IMessage
    {
        byte[] Bytes { get; }
    }
    public interface IMessageFactory<M>
    {
        M FromBytes(byte[] bytes);
    }
    public interface ICanReceiveMessage<M>
    {
        void Tell(M message, ICanReceiveMessage<M> sender);
        Task<M> Ask(M message, IMessageFactory<M> factory, TimeSpan? timeout = null, System.Threading.CancellationToken? cancellationToken = null);
    }
    public interface IMessagePattern<M>
    {
        string[] Conjuncts { get; }
        bool IsSecondary { get; }
        bool Match(M message);
    }
    public interface IMessagePatternFactory<P>
    {
        P FromConjuncts(string[] conjuncts, bool isSecondary=false);
    }

    public class MessageProps
    {
        public Guid CorrelationId { get; private set; }

        public static MessageProps New(MessageProps origin = null)
        {
            return new MessageProps
            {
                CorrelationId = origin == null ? Guid.NewGuid() : origin.CorrelationId
            };
        }
    }
}

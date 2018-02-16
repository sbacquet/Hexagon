using System;
using System.Threading.Tasks;

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
        M FromString(string content);
    }
    public interface ICanReceiveMessage<M>
    {
        void Tell(M message, ICanReceiveMessage<M> sender);
        Task<M> AskAsync(M message, IMessageFactory<M> factory, TimeSpan? timeout = null, System.Threading.CancellationToken? cancellationToken = null);
        string Path { get; }
    }
    public interface IMessagePattern<M>
    {
        string[] Conjuncts { get; }
        bool IsSecondary { get; }
        bool Match(M message);
        (string[], bool) ToTuple();
    }
    public interface IMessagePatternFactory<P>
    {
        P FromConjuncts(string[] conjuncts, bool isSecondary=false);
    }
}

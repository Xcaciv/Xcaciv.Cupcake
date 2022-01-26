
namespace Xcaciv.Cupcake.Core.Exceptions
{
    public class LoadingException : Exception
    {
        public LoadingException(string message) : base(message)
        {
        }

        public LoadingException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
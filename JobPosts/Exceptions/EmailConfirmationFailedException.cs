namespace JobPosts.Exceptions
{
    public class EmailConfirmationFailedException : Exception
    {
        public EmailConfirmationFailedException(string message)
        : base(message) { }
    }
}

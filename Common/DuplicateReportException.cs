namespace BookSharingApp.Common
{
    public class DuplicateReportException : Exception
    {
        public DuplicateReportException() : base("You have already reported this message") { }
    }
}

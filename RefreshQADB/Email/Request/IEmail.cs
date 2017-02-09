namespace RefreshQADB.Email.Request
{
    public interface IEmail
    {
        int SendMail(string subject, string body);
    }
}

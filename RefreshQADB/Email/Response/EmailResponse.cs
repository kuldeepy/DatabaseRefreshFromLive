namespace RefreshQADB.Email.Response
{
    public class EmailResponse
    {
        public int EmailQueueId { get; set; }
        public string Status { get; set; }
        public object ErrorMessage { get; set; }
        public object InvalidValue { get; set; }
        public object UpdatedTime { get; set; }
        public int Timestamp { get; set; }
    }
}

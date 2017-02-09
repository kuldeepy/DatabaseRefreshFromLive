using System;
using System.Collections.Generic;
using System.Configuration;
using System.Text;
using RefreshQADB.Email.Response;
using System.Net.Http;
using Newtonsoft.Json;

namespace RefreshQADB.Email.Request
{
    public class Email : IEmail
    {
        private string _emailServiceUrl = string.Empty;

        private string _emailToken = string.Empty;

        private string _fromAddress = string.Empty;

        private string _toAddress = string.Empty;

        private List<object> _listToAddress = null;

        public Email()
        {
            _emailServiceUrl = ConfigurationManager.AppSettings["EmailServiceUrl"];
            _emailToken = ConfigurationManager.AppSettings["EmailSecurityToken"];
            _fromAddress = ConfigurationManager.AppSettings["emailErrorsFrom"];
            _toAddress = ConfigurationManager.AppSettings["emailErrorsTo"];
            GetListOfReceiver();
        }

        public int SendMail(string subject, string body)
        {
            //Logger logger = LogManager.GetCurrentClassLogger();
            int status = 0;
            try
            {
                var fromAddress = new { EmailAddress = _fromAddress };
                using (HttpClient client = new HttpClient())
                {

                    var value = new
                    {
                        SecurityToken = _emailToken,
                        Subject = subject,
                        BodyContent = body,
                        BodyContentType = "html",
                        FromAddress = fromAddress,
                        ToAddresses = _listToAddress,
                        Source = new
                        {
                            ApplicationName = "EmailLoggingClient",
                            ModuleName = "SendMail",
                            MachineName = Environment.MachineName
                        },
                    };
                    var httpContent = new StringContent(JsonConvert.SerializeObject(value), Encoding.UTF8, "application/json");
                    var response = client.PostAsync(_emailServiceUrl, httpContent).Result;
                    if (response.IsSuccessStatusCode)
                    {
                        var result = JsonConvert.DeserializeObject<EmailResponse>(response.Content.ReadAsStringAsync().Result);
                        status = result.EmailQueueId;
                    }
                }
            }
            catch (Exception ex)
            {
                //logger.Error(ex, "Error occurred while sending mail.");
            }
            return status;
        }

        private List<object> GetListOfReceiver()
        {
            if (!string.IsNullOrEmpty(_toAddress))
            {
                _listToAddress = new List<object>();
                var splitedToAddress = _toAddress.Split(',');
                foreach (var address in splitedToAddress)
                {
                    _listToAddress.Add(new { EmailAddress = address });
                }
            }
            return _listToAddress;
        }
    }
}

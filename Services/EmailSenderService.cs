using Alejandria.Server.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Text;

namespace Alejandria.Server.Services
{
    public class EmailSenderService
    {
        private static string _Host = "smtp-mail.outlook.com";
        private static int _Puerto = 587;

        private static string _NameFrom = "No-Reply";
        private static string _MailFrom = "noreply@alexandreya.com";
        private static string _key = "u78%RED4e_rv";

        public static bool Enviar(CorreoDTO correodto)
        {
            try
            {
                var email = new MimeMessage();

                email.From.Add(new MailboxAddress(_NameFrom, _MailFrom));
                email.To.Add(MailboxAddress.Parse(correodto.Para));
                email.Subject = correodto.Asunto;
                email.Body = new TextPart(TextFormat.Html)
                {
                    Text = correodto.Contenido
                };

                var smtp = new SmtpClient();
                smtp.Connect(_Host, _Puerto, SecureSocketOptions.StartTls);

                smtp.Authenticate(_MailFrom, _key);
                smtp.Send(email);
                smtp.Disconnect(true);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }
    }
}
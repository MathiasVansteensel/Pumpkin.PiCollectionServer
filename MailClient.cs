using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Pumpkin.PiCollectionServer;
internal static class MailClient //using https://app.elasticemail.com/
{
	//I know, not very secure, 2min with cheat engine and you could have the password... but idc i dont have time for "SecureString" of smth similar
	private const string Password = "3A6AC4DBB90E3C0D3E59289C4A30B8FADD6B";
	private const string Username = "pumpkin.smarthome@gmail.com";
	private const ushort Port = 2525;
	private const string Server = "smtp.elasticemail.com";

	public static event EventHandler<MailMessage> MessageSent;
	public static async void SendEmail(string recipientEmail, string subject, string body)
	{
		SmtpClient smtpClient = new SmtpClient
		{
			Host = Server,
			Port = Port,
			EnableSsl = true,
			Credentials = new NetworkCredential(Username, Password)
		};

		MailMessage message = new MailMessage 
		{
			From = new(Username),
			Subject = subject,
			Body = body
		};
		message.To.Add(new MailAddress(recipientEmail));

		smtpClient.SendCompleted += (sender, e) => SmtpClient_SendCompleted(smtpClient, message);

		await smtpClient.SendMailAsync(message);
	}

	private static void SmtpClient_SendCompleted(SmtpClient client, MailMessage sentMessage)
	{
		MessageSent?.Invoke(client, sentMessage);
		client?.Dispose();
	}
}

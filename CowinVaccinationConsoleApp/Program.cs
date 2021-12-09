using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using Newtonsoft.Json;

namespace CowinVaccinationConsoleApp
{

    public class Program
    {

        private static readonly HttpClient client = new HttpClient();

        static async Task Main(string[] args)
        {
            await ProcessVaccination();
        }

        private static async Task ProcessVaccination()
        {
            Cowin cowin = new Cowin();
            ConfirmOtp confirmOtp = new ConfirmOtp();
            try
            {
                Console.WriteLine("Enter your mobile number");
                var mobile = Console.ReadLine();
                var transaction = await cowin.GetTransactoinID(mobile);
                if (transaction != null)
                {
                    confirmOtp.txnId = transaction.txnId;

                    Console.WriteLine("Enter your otp");
                    confirmOtp.otp = Console.ReadLine();
                    var token = await cowin.GetToken(confirmOtp);

                    if (token != null)
                    {
                        Console.WriteLine("Enter Beneficiary Ref. Id");
                        var bfRefId = Console.ReadLine();
                        var pdffile = await cowin.DownloadPdf(bfRefId, token.token);
                        //Console.WriteLine(pdffile);
                        Console.Read();
                    }
                }
                else
                {
                    Console.WriteLine("Not able to fetch data, please try after sometime");
                    await ProcessVaccination();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Not able to fetch data, please try after sometime");
                await ProcessVaccination();
            }
        }
    }
    public class Cowin
    {
        public async Task<Transaction> GetTransactoinID(string mobileNumber)
        {
            Transaction txn = new Transaction();
            using (var client = new HttpClient())
            {
                var message = JsonConvert.SerializeObject(new { mobile = mobileNumber });
                var res = await client.PostAsync("https://cdn-api.co-vin.in/api/v2/auth/public/generateOTP",
                  new StringContent(message,
                    Encoding.UTF8, "application/json")
                );

                try
                {
                    txn = JsonConvert.DeserializeObject<Transaction>(res.Content.ReadAsStringAsync().Result);
                }
                catch (Exception e)
                {
                    throw;
                }
            }

            return txn;
        }
        public async Task<Token> GetToken(ConfirmOtp confirmOtp)
        {
            Token token = new Token();
            using (var client = new HttpClient())
            {
                confirmOtp.otp = sha256(confirmOtp.otp);
                var message = JsonConvert.SerializeObject(new { txnId = confirmOtp.txnId, otp = confirmOtp.otp });
                var res = await client.PostAsync("https://cdn-api.co-vin.in/api/v2/auth/public/confirmOTP",
                  new StringContent(message,
                    Encoding.UTF8, "application/json")
                );

                try
                {
                    token = JsonConvert.DeserializeObject<Token>(res.Content.ReadAsStringAsync().Result);
                }
                catch (Exception e)
                {
                    throw;
                }
            }

            return token;
        }
        public async Task<string> DownloadPdf(string beneficiaryRefId, string token)
        {
            string binaryPDF = string.Empty;
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri("https://cdn-api.co-vin.in/");
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var requestUri = new Uri($"api/v2/registration/certificate/public/download?beneficiary_reference_id={HttpUtility.UrlEncode(beneficiaryRefId)}", UriKind.Relative);
                var res = client.GetAsync(requestUri);
                var result = res.Result.Content.ReadAsStringAsync().Result;
                string filename = string.Empty;
                using (var file = System.IO.File.Create("CovidCerticate.pdf"))
                { 
                    filename = file.Name;
                    var contentStream = await res.Result.Content.ReadAsStreamAsync(); // get the actual content stream
                    await contentStream.CopyToAsync(file);                                 
                }

                //using (PdfReader reader = new PdfReader(filename))
                //{
                //    StringBuilder text = new StringBuilder();

                //    for (int i = 1; i <= reader.NumberOfPages; i++)
                //    {
                //        text.Append(PdfTextExtractor.GetTextFromPage(reader, i));
                //    }


                    var pdfExtract =  PdfTextExtract.pdfText(filename);

                    binaryPDF = pdfExtract.ToString();

                    //string contents = sr.ReadToEnd();
                    if (binaryPDF.Contains("2/2") && binaryPDF.Contains("1/2"))
                    {
                        Console.WriteLine("");
                        Console.WriteLine("*************************");
                        Console.WriteLine("You are fully vaccinated");
                        Console.WriteLine("*************************");
                        Console.WriteLine("");
                    }
                    else if(binaryPDF.Contains("1/2"))
                    {
                        Console.WriteLine("");
                        Console.WriteLine("**********************");
                        Console.WriteLine("You are partially vaccinated");
                        Console.WriteLine("**********************");
                        Console.WriteLine("");
                    }
                    else
                    {
                        Console.WriteLine("");
                        Console.WriteLine("**********************");
                        Console.WriteLine("You are not Vaccinated");
                        Console.WriteLine("**********************");
                        Console.WriteLine("");
                    }
                
                if (File.Exists(filename))
                {
                    // If file found, delete it    
                    File.Delete(filename);
                    Console.WriteLine("");
                    Console.WriteLine("");
                    Console.WriteLine("File deleted.");
                }
            }
            return binaryPDF;
        }
        public static class PdfTextExtract
        {
            public static string pdfText(string result)
            {
                PdfReader reader = new PdfReader(result);
                string text = string.Empty;
                for (int page = 1; page <= reader.NumberOfPages; page++)
                {
                    text += PdfTextExtractor.GetTextFromPage(reader, page);
                }
                reader.Close();
                return text;
            }
        }
        static string sha256(string randomString)
        {
            var crypt = new System.Security.Cryptography.SHA256Managed();
            var hash = new System.Text.StringBuilder();
            byte[] crypto = crypt.ComputeHash(Encoding.UTF8.GetBytes(randomString));
            foreach (byte theByte in crypto)
            {
                hash.Append(theByte.ToString("x2"));
            }
            return hash.ToString();
        }
    }
    public class Transaction
    {
        public string txnId { get; set; }
    }
    public class ConfirmOtp
    {
        public string otp { get; set; }
        public string txnId { get; set; }
    }
    public class Token
    {
        public string token { get; set; }
        public string isNewAccount { get; set; }
    }
}

#r "Newtonsoft.Json"
#r "System.Drawing"
#r "Microsoft.WindowsAzure.Storage"
 
using System;
using System.Net;
using Newtonsoft.Json;
using System.Drawing;
using System.IO;
using System.Linq;
using Microsoft.WindowsAzure.Storage;
 
public static async Task<object> Run(HttpRequestMessage req, TraceWriter log)
{
    try{
        log.Info($"Webhook was triggered!");
 
        //extract parameters
        string formDataStr = await req.Content.ReadAsStringAsync();
        string[] data = formDataStr.Split('&');
 
        var textParam = data.SingleOrDefault(m => m.StartsWith("text="));
 
        textParam = WebUtility.UrlDecode(textParam.Substring(5,textParam.Length-5));
        log.Info(textParam);
        var parameters = textParam.Split('~').ToArray();
        foreach(var parameter in parameters){
            log.Info(parameter);
        }
        var emotion = parameters[0].Trim();
        var topText = parameters[1].Trim();
        var bottomText = parameters[2].Trim();
        var templateUrl = $"https://BLOBURL/{emotion}.JPG";
        log.Info(templateUrl);
        //create the bitmap
        var bitmap = CreateMeme(templateUrl,topText,bottomText);
       
        //setup storage account
        var storageAccount = CloudStorageAccount.Parse("DefaultEndpointsProtocol=https;AccountName=ACCOUNTNAME;AccountKey=ACCOUNTKEY;EndpointSuffix=core.windows.net");
        var blobClient = storageAccount.CreateCloudBlobClient();
        var container = blobClient.GetContainerReference("CONTAINER");
        container.CreateIfNotExists();
        var blockBlob = container.GetBlockBlobReference($"BLOBPREFIX{Guid.NewGuid()}.png");
 
        //upload the blob
        using (var stream = new MemoryStream())
        {
            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            stream.Position = 0;
            blockBlob.Properties.ContentType = "image/png";
            blockBlob.UploadFromStream(stream);
        }
 
        //respond once completed
        var responseUrlParam = data.SingleOrDefault(m => m.StartsWith("response_url="));
        log.Info(responseUrlParam);
        responseUrlParam = WebUtility.UrlDecode(responseUrlParam.Substring(13,responseUrlParam.Length-13));
        log.Info(responseUrlParam);
        using (var client = new HttpClient())
        {
            client.BaseAddress = new Uri(responseUrlParam);
            var result = await client.PostAsJsonAsync("",new
            {
                response_type= "in_channel",
                text = blockBlob.Uri.AbsoluteUri
            });
   
            log.Info(result.StatusCode.ToString());
        }
        return req.CreateResponse(HttpStatusCode.OK);
    }
    catch{
        return req.CreateResponse(HttpStatusCode.OK, new {
            text = "Use format \"/SLACKCOMMAND {emotion} ~topText ~bottomText\" where {emotion} is one of the image names found at https://BLOBURL/CHEATSHEET.JPG"
        });
    }
}
 
//Adds text to image in the style of a meme
private static Bitmap CreateMeme(string imageUrl, string topText, string bottomText)
{
    topText = topText.ToUpper();
    bottomText = bottomText.ToUpper();
 
    var wc = new WebClient();
    var bytes = wc.DownloadData(imageUrl);
    var ms = new MemoryStream(bytes);
    var bitmap = (Bitmap)Image.FromStream(ms);
 
    float maxPixelSize = 144;
    var pixelSizeTop = Math.Min(bitmap.Width / (float)topText.Length * 1.7f, maxPixelSize);
    var pixelSizeBottom = Math.Min(bitmap.Width / (float)bottomText.Length * 1.7f, maxPixelSize);
 
    using (var graphics = Graphics.FromImage(bitmap))
    {
        StringFormat stringFormat = new StringFormat();
        stringFormat.Alignment = StringAlignment.Center;
        stringFormat.LineAlignment = StringAlignment.Center;
        using (var font = new Font("Impact", pixelSizeTop, FontStyle.Bold, GraphicsUnit.Pixel))
        {
            graphics.DrawString(topText, font, Brushes.White, new Rectangle(0, 0, bitmap.Width, Convert.ToInt32(pixelSizeTop)), stringFormat);
        }
        using(var font = new Font("Impact", pixelSizeBottom, FontStyle.Bold, GraphicsUnit.Pixel))
        {
            graphics.DrawString(bottomText, font, Brushes.White, new Rectangle(0, bitmap.Height - Convert.ToInt32(pixelSizeBottom), bitmap.Width, Convert.ToInt32(pixelSizeBottom)), stringFormat);
        }
    }
 
    return bitmap;
}
# Run and Debug Bot locally

In our scenario, we will have a Virtual Machine running a bot where the bot belongs to a specific domain which is configured to work with Microsoft Teams.
To debug the bot code locally, you will require setting it up on your local machine first, which this documentation tries to address.

---

## Pre-requisites for debugging bot locally

1. Installed Certificate
  - You need to have a wildcard SSL certificate for the domain (like *.mybot.com) or sub-domain certificate (like bot.website.com) .  
  - **Note:** An SSL certificate can be found in the Key Vault. Download it as a PFX file into the local machine (do not put a value in the password field)
  - This certificate must be installed in your system certificate manager.
2. Ngrok Account
  - Signup for [ngrok free account](ngrok.com) or login and get auth token.
3. Azure Speech API key
4. Run the Inference API as instructed in `src/inference`

### Setting up the code
1. Install VS Community with ASP.NET while installing
2. Add [NuGet Package Manager](https://stackoverflow.com/a/58945739) and [install packages](https://stackoverflow.com/a/48440777)

### Setting up Azure Bot Service

Register an Azure bot and add "Teams" channel, and give all "Calls"-related permissions. Refer this:  
https://microsoftgraph.github.io/microsoft-graph-comms-samples/docs/articles/calls/register-calling-bot.html

For both meeting hosting tenant as well as bot service domain, tenant admin needs to grant permissions.  
Go to the following URL (as admins) to give consent:  
https://login.microsoftonline.com/<TENANT_ID>/adminconsent?client_id=<APP_ID>&state=0&redirect_uri=<BOT_DOMAIN>

- `TENANT_ID` can be obtained from _Overview_ section of _Azure Active Directory_
- `APP_ID`: Bot ID

### Setting up a Virtual Camera

In-case you don't know to sign (or feeling shy), install [OBS Studio](https://obsproject.com/download), and restart your computer.

1. Open it and create a new source
2. Add a media (some signer's video) & mark it as loop, and start the Virtual camera.
3. In the meeting call, choose "OBS VirtualCam" as video source

---

## Steps to debug

### Setup and run Ngrok
1. Have the following ngrok configuration in a file. (Check `scripts/ngrok.yml`)
    
    ```yaml
    authtoken: YOUR_TOKEN
    tunnels:
        signaling:
            addr: https://localhost:9441
            proto: http
        media:
            addr: 8445
            proto: tcp
    ```

2. Run ngrok using `ngrok start --all --config=ngrok.yml` (or `scripts\run_grok.bat scripts\ngrok.yml`).  
  You should see an output like this:
  ![ngrok](./images/ngrok.png)

  Note down the following thing from ngrok output  
    - http forwarding url (something.ngrok.io) 
    - tcp forwarding address (0.tcp.ngrok.io) and 
    - its port 14211.  

### Installing SSL Certificate

1. Generate an SSL Certificate somehow for your sub-domain, and have it in `.pfx` format.
2. [Install the certificate on Windows](https://support.securly.com/hc/en-us/articles/360026808753-How-do-I-manually-install-the-Securly-SSL-certificate-on-Windows)
  - Note: Install it under "Personal", not "Trusted Root Certification Authorities"
  - Ensure you have given [read permission to the private key to the current Windows user](https://stackoverflow.com/a/22640086). 
3. After installing the certificate, go to properties of that certificate and copy the thumbprint & put it in the JSON.

#### How to generate Let's Encrypt SSL Certificates

1. [Follow this tutorial](https://www.digitalocean.com/community/tutorials/how-to-secure-apache-with-let-s-encrypt-on-ubuntu-20-04) (or any other guide of your choice).
2. Get the `cert.pem` from `/etc/letsencrypt` (depends on OS) and convert it to `.pfx` format using OpenSSL command (Google it). (Or use this: https://www.httpcs.com/en/ssl-converter )

### Set up a CNAME 

Youll need an alias from a public domain that Microsoft teams sees to a local domain.  
One way to do this is to have a domain and a CNAME that points to the TCP tunnel running locally.  
(Azure DNS also provides hosting for a domain.)

Steps:

1. Setup a CNAME that points to the *tcp* address of ngrok. Ngrok tcp are usually `0.tcp...` `1.tcp...` `2.tcp...` etc.
  If you only have one subdomain mapped to `x.tcp` and if ngrok did not assign that same `x.tcp` address, then just re-run it till it does.
  Finally what we need from here is the port number.
2. Add the CNAME in the app settings

### Setup Application Settings

1. Below are the changes for `appsettings.json`. 
    - Update to the `ServiceCname` the ngrok http address (above) 
    - Update the `InstancePublicPort` to ngrok tcp assigned port (above). 
    - `MediaServiceFQDN` is the subdomain URL that is forwared to tcp URL assigned by ngrok (`local.mybot.com` is forwarded to `0.tcp.ngrok.io`).
    - `CertificateThumbprint` should either be wildcard certificate (from above) or certificate for subdomain you are forwarding from, in this case it�s `*.mybot.com`. If you are using more nested sub domain for CNAME forwarding to ngrok tcp then the certificate should be for 1 domain higher than the nest depth. For example, if your CNAME entry is `0.local.mybot.com` then you need certificate for `*.local.mybot.com`. Having a certificate for `*.mybot.com` does not work.
    ```yaml
    "AzureSettings": {
        "BotName": "BOT_NAME",
        "AadAppId": " APP_ID",
        "AadAppSecret": "APP_SECRET",
        "ServiceCname": "87be4e797dbe.ngrok.io",
        "MediaServiceFQDN": "local.mybot.com",
        "ServiceDnsName": "",
        "CertificateThumbprint": "CERTIFICATE_THUMBPRINT",
        "InstancePublicPort": 14211,
        "CallSignalingPort": 9441,
        "InstanceInternalPort": 8445,
        "PlaceCallEndpointUrl": https://graph.microsoft.com/v1.0
     }
    ```

### Run and debug

1. Run the project - make sure you have the startup as the AI4Bharat.ISLBot.Services (run in Kestrel not IIS Express)
2. Check that the debug window for Visual Studio doesnt contain errors
3. Check on ngrok that messages are being tunnelled correctly (no errors)
4. Choose or create a Microsoft teams meeting and find the URL
5. Switch on your video in the Microsoft Teams Meeting
6. You will need POSTMan (or equivilent) to add the bot to the meeting  

    https://YOUR_DNS.ngrok.io/joinCall

    raw body exmple
    ```
    {
    "JoinURL": "https://teams.microsoft.com/l/meetup-join/YOUR_MEETING_ID"
    }
    ```

    Make a note of the logs file in the response.  
    You can observe the video being recorded in the log file (https://YOUR_DNS.ngrok.io/logs/YOUR_LOG_FILE)
7. The bot would have started a screen-sharing and transcribing the signs from the video.
8. To remove the bot from call:  
  POST DELETE to http://YOUR_DNS.ngrok.ui/calls/YOUR_CALL_ID

Alternatively, you can go to https://SUBDOMAIN.ngrok.com/manage to join/remove a bot to a meeting.

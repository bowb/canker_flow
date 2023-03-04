
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Web;
using static System.Net.WebRequestMethods;
using System.Reflection.PortableExecutable;
using System.Net.Http.Headers;
using static System.Net.Mime.MediaTypeNames;
using canker.Model;
using System.ComponentModel.Design;

namespace canker;

public partial class MainPage : ContentPage
{
	
	public MainPage()
	{
		InitializeComponent();
	}

	private async void Go(object sender, EventArgs e)
    {
        try
        {
            var loginData = (dynamic)new JObject();
            loginData.password = "";
            loginData.username = username.Text;

            var test = loginData.ToString();

            // first request empty password get the stateToken
            var client = new HttpClient();
            var content = new StringContent(loginData.ToString(), Encoding.UTF8, "application/json");
            HttpResponseMessage response = await client.PostAsync("https://id.churchofjesuschrist.org/api/v1/authn", content);

            response.EnsureSuccessStatusCode();
            var jsonString = await response.Content.ReadAsStringAsync();
            dynamic stateData = JsonConvert.DeserializeObject<object>(jsonString);
            var stateToken = stateData.stateToken.ToString();

            // get the sessionToken
            loginData.password = password.Text;
            content = new StringContent(loginData.ToString(), Encoding.UTF8, "application/json");
            response = await client.PostAsync("https://id.churchofjesuschrist.org/api/v1/authn", content);

            response.EnsureSuccessStatusCode();
            jsonString = await response.Content.ReadAsStringAsync();
            dynamic sessionData = JsonConvert.DeserializeObject<object>(jsonString);
            var sessionToken = sessionData.sessionToken.ToString();

            // need to get the code value from the Locateion header from authorize
            var uuid = Guid.NewGuid();
            var authorizeUrl = "https://id.churchofjesuschrist.org/oauth2/default/v1/authorize?client_id=0oalh46uylP0G9QY1357&response_type=code&scope=openid%20profile%20offline_access%20cmisid&redirect_uri=https://mobileandroid&state=" + uuid.ToString() + "&sessionToken=" + sessionToken;

            var handler = new HttpClientHandler()
            {
                AllowAutoRedirect = false
            };
            var noRedirectClient = new HttpClient(handler);

            response = await noRedirectClient.GetAsync(authorizeUrl);
            string location = response.Headers.GetValues("location").FirstOrDefault();

            // example
            // https://mobileandroid?code=KuOJZjc2kQecLS-qIqr9zFbLpsZ9lZsKJHhA1W_9vNU&state=5f6248dd-31a3-4a45-bc33-0ed0c1c1bf76

            Uri locationUri = new Uri(location);
            string code = HttpUtility.ParseQueryString(locationUri.Query).Get("code");

            // get the bearer token
            var bearerUrl = "https://id.churchofjesuschrist.org/oauth2/default/v1/token";
            var prms = new Dictionary<string, string>();
            prms.Add("code", code);
            prms.Add("client_id", "0oalh46uylP0G9QY1357");
            prms.Add("client_secret", "9a4FuuOtkz17um4O8UIG3eFI4uWiamKU1owUxZCE");
            prms.Add("grant_type", "authorization_code");
            prms.Add("redirect_uri", "https://mobileandroid");
            var request = new HttpRequestMessage(HttpMethod.Post, bearerUrl) { Content = new FormUrlEncodedContent(prms) };

            request.Headers.Add("Accept-Charset", "UTF-8");
            response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            jsonString = await response.Content.ReadAsStringAsync();
            dynamic authData = JsonConvert.DeserializeObject<object>(jsonString);
            var accessToken = authData.access_token.ToString();

            using (var requestMessage =
            new HttpRequestMessage(HttpMethod.Get, "https://membertools-api.churchofjesuschrist.org/api/v4/units"))
            {
                requestMessage.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", accessToken);
                response = await client.SendAsync(requestMessage);
                response.EnsureSuccessStatusCode();
                jsonString = await response.Content.ReadAsStringAsync();
            }

            dynamic unitData = JsonConvert.DeserializeObject<object>(jsonString);
            var reqMemberData = (dynamic)new JArray();
            var types = (dynamic)new JArray();
            var unitNumbers = (dynamic)new JArray();
            var reqObject = (dynamic)new JObject();

            foreach (var unit in unitData)
            {
                unitNumbers.Add(unit.unitNumber);
                foreach (var cunit in unit.childUnits)
                {
                    unitNumbers.Add(cunit.unitNumber);
                }
            }

            types.Add("HOUSEHOLDS");
            types.Add("ORGANIZATIONS");
            reqObject.types = types;
            reqObject.unitNumbers = unitNumbers;
            reqMemberData.Add(reqObject);

            var getMembersUrl = "https://membertools-api.churchofjesuschrist.org/api/v4/sync?force=true";
            var membersJsonString = "";
            using (var requestMessage =
            new HttpRequestMessage(HttpMethod.Post, getMembersUrl))
            {
                requestMessage.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", accessToken);
                content = new StringContent(reqMemberData.ToString(), Encoding.UTF8, "application/json");
                requestMessage.Content = content;
                response = await client.SendAsync(requestMessage);
                response.EnsureSuccessStatusCode();
                membersJsonString = await response.Content.ReadAsStringAsync();
            }

            JObject rss = JObject.Parse(membersJsonString);

            var mm = (from hh in rss["households"]
                      select hh["members"]).ToList();

            var miList = new List<MemberInfo>();

            foreach(var m in mm)
            {
                foreach (var i in m)
                {
                    var email = "";
                    if (null == i["email"])
                    {
                        continue;
                    }

                    email = i["email"].ToString();

                    var fname = "";
                    if (null != i["givenName"])
                    {
                        fname = i["givenName"].ToString();
                    }
                    var lname = "";
                    if (null != i["familyName"])
                    {
                        lname = i["familyName"].ToString();
                    }

                    var mi = new MemberInfo
                    {
                        email = email,
                        fname = fname,
                        lname = lname
                    };

                    miList.Add(mi);
                }
            }

            await Navigation.PushAsync(new Results(miList));

        }
        catch (Exception ex)
        {
            error.Text = "An error has happened:"  + ex.Message;
        }
    }
}


using System.Collections.ObjectModel;
using System.IO;
using System.Diagnostics;
using System.Text.Json;
using System.Security.Cryptography;
using System.Windows.Input;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;

namespace DaatAharon.Desktop;

public sealed class RouteRule { public string Host { get; set; } = ""; public string Path { get; set; } = "/"; public bool Allowed { get; set; } }
public sealed class ElementRule { public string Host { get; set; } = ""; public string Selector { get; set; } = ""; public bool Allowed { get; set; } public string ReplacementText { get; set; } = ""; public string ReplacementImagePath { get; set; } = ""; public string ReplacementImageFit { get; set; } = "contain"; }
public sealed class LocalPolicy {
 public List<RouteRule> Routes { get; set; } = [];
 public List<string> AllowedHosts { get; set; } = ["donors.matara.pro","www.matara.pro","matara.pro","rishumon.net","tora.irom.co.il"];
 public List<ElementRule> Elements { get; set; } = [];
 public List<string> HiddenServiceIds { get; set; } = [];
 public List<string> FavoriteServiceIds { get; set; } = [];
 public Dictionary<string,int> ClickCounts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
 public bool AllowSameOriginPopups { get; set; } = true;
 public string Theme { get; set; } = "midnight";
 public string BackgroundImagePath { get; set; } = "";
}
public sealed record PortalService(string Id,string Title,string Subtitle,string Url,string Category);

public partial class MainWindow : Window {
 private static readonly string DataFolder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DaatAharon");
 private static readonly string PolicyPath=Path.Combine(DataFolder,"policy.json");
 private static readonly JsonSerializerOptions JsonOptions=new(){WriteIndented=true,PropertyNameCaseInsensitive=true};
 private LocalPolicy _policy=LoadPolicy();
 private readonly ObservableCollection<RouteRule> _rules=[];
 private readonly ObservableCollection<ElementRule> _elements=[];
 private readonly List<string> _events=[];
 private bool _elementPicking;
 private bool _adminUnlocked;
 private const int PasswordIterations=310000;
 private static readonly string PasswordPath=Path.Combine(DataFolder,"admin-password.json");
 private static readonly string LegacyPasswordPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"RavKavKiosk","admin-password.json");
 private static readonly PortalService[] Services=[
  new("nedarim","נדרים פלוס – אזור אישי","כניסה לאזור האישי","https://donors.matara.pro/login","שירותים"),
  new("torah-dileyh","תורה דיליה","סיכומי סוגיות וכתיבת חבורות","https://www.matara.pro/nedarimplus/online/?mosad=7005288","לימוד"),
  new("prints","הטענת הדפסות","טעינת יתרת צילום והדפסה","https://www.matara.pro/nedarimplus/Forms/4488.html?MasofId=&ClientId=&Zeout=&Version=1","שירותים"),
  new("dirshu","מבחני דרשו","ציונים, מועדי בחינות והרשמה","https://matara.pro/nedarimplus/online/?mosad=7005954","מבחנים"),
  new("kivro","ישיבה על קברו","מפעל הלימוד וההנצחה","https://www.matara.pro/nedarimplus/v6/Mokirim14.html?Version=1","לימוד"),
  new("ladaat","מבחני לדעת","מגוון מבחנים","https://www.matara.pro/nedarimplus/Forms/976.html?MasofId=&ClientId=&Zeout=&Version=8","מבחנים"),
  new("siach","שיח התורה","מבחנים בכל סגנונות התורה","https://rishumon.net/SiachAtorah.html","מבחנים"),
  new("torah-written","תורה שבכתב","עיון בטקסטים, מפרשים ודקדוק","https://tora.irom.co.il/?Version=17","לימוד"),
  new("hopon","טעינת רב־קו HopOn","פתיחת שירות הטעינה","https://hopon.co.il/read","רב־קו"),
  new("ravkav","רב־קו אונליין","חיבור לכרטיס וטעינה","https://ravkavonline.co.il/he/store/connect","רב־קו")
 ];
 public MainWindow() {
  InitializeComponent(); RulesGrid.ItemsSource=_rules; ElementsGrid.ItemsSource=_elements; AllowPopupsCheck.IsChecked=_policy.AllowSameOriginPopups;
  BuildPortal(); RefreshCounts(); ApplyTheme(); ApplyBackground(); ClockText.Text=DateTime.Now.ToString("dddd, dd MMMM yyyy · HH:mm");
  Loaded+=async (_,_)=>await InitializeBrowserAsync();
  Closing+=(_,_)=>{try{Browser.Dispose();}catch{}};
 }
 private static LocalPolicy LoadPolicy() {
  try {
   Directory.CreateDirectory(DataFolder);
   foreach(var candidate in new[]{PolicyPath,PolicyPath+".bak",Path.Combine(DataFolder,"RavKavKiosk","admin-policy.json"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"RavKavKiosk","admin-policy.json")}) {
    if(!File.Exists(candidate))continue;
    var loaded=JsonSerializer.Deserialize<LocalPolicy>(File.ReadAllText(candidate),JsonOptions);
    if(loaded is not null) {
     if(!string.Equals(candidate,PolicyPath,StringComparison.OrdinalIgnoreCase)) {
      File.WriteAllText(PolicyPath,JsonSerializer.Serialize(loaded,JsonOptions));
      LogStatic("הגדרות מהתוכנה הישנה הועברו בהצלחה",null);
     }
     return loaded;
    }
   }
  } catch(Exception ex){LogStatic("טעינת מדיניות נכשלה",ex);}
  return new LocalPolicy();
 }
 private static void LogStatic(string message,Exception? ex=null) { try { Directory.CreateDirectory(DataFolder); File.AppendAllText(Path.Combine(DataFolder,"activity.log"),$"[{DateTime.Now:O}] {message} {ex}\n"); } catch {} }
 private void SavePolicy() {
  Directory.CreateDirectory(DataFolder); string tmp=PolicyPath+".tmp";
  File.WriteAllText(tmp,JsonSerializer.Serialize(_policy,JsonOptions));
  if(File.Exists(PolicyPath))File.Copy(PolicyPath,PolicyPath+".bak",true);
  File.Move(tmp,PolicyPath,true); RefreshCounts(); Log("מדיניות נשמרה");
 }
 private void BuildPortal() {
  PortalCards.Children.Clear();
  string search=PortalSearchBox?.Text?.Trim()??"";
  var visible=Services.Where(service=>!_policy.HiddenServiceIds.Contains(service.Id,StringComparer.OrdinalIgnoreCase))
   .Where(service=>string.IsNullOrWhiteSpace(search)||(service.Title+" "+service.Subtitle+" "+service.Category).Contains(search,StringComparison.CurrentCultureIgnoreCase))
   .OrderByDescending(service=>_policy.FavoriteServiceIds.Contains(service.Id,StringComparer.OrdinalIgnoreCase))
   .ThenBy(service=>service.Title);
  foreach(var service in visible) {
   bool favorite=_policy.FavoriteServiceIds.Contains(service.Id,StringComparer.OrdinalIgnoreCase);
   var panel=new StackPanel();
   panel.Children.Add(new TextBlock{Text=(favorite?"★  ":"")+service.Title,FontSize=16,FontWeight=FontWeights.Bold,Foreground=System.Windows.Media.Brushes.White});
   panel.Children.Add(new TextBlock{Text=service.Subtitle,Foreground=System.Windows.Media.Brushes.LightGray,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,5,0,0)});
   panel.Children.Add(new TextBlock{Text="נפתח "+(_policy.ClickCounts.TryGetValue(service.Id,out int count)?count:0)+" פעמים",Foreground=System.Windows.Media.Brushes.LightSteelBlue,FontSize=10,Margin=new Thickness(0,8,0,0)});
   var button=new Button{Content=panel,Tag=service,Padding=new Thickness(16),Margin=new Thickness(0,0,10,10),Background=new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(27,43,66)),BorderThickness=new Thickness(0),HorizontalContentAlignment=HorizontalAlignment.Stretch};
   button.Click+=(_,_)=>{_policy.ClickCounts[service.Id]=_policy.ClickCounts.TryGetValue(service.Id,out int n)?n+1:1;SavePolicy();OpenService(service);};
   var menu=new ContextMenu();
   var favoriteItem=new MenuItem{Header=favorite?"הסר ממועדפים":"הוסף למועדפים"};
   favoriteItem.Click+=(_,_)=>{if(!_policy.FavoriteServiceIds.Remove(service.Id))_policy.FavoriteServiceIds.Add(service.Id);SavePolicy();BuildPortal();};
   menu.Items.Add(favoriteItem);
   var hideItem=new MenuItem{Header="הסתר שירות (מנהל)"};
   hideItem.Click+=(_,_)=>{if(!_adminUnlocked){MessageBox.Show("הסתרת שירותים זמינה באזור המנהל בלבד.");return;}_policy.HiddenServiceIds.Add(service.Id);SavePolicy();BuildPortal();};
   menu.Items.Add(hideItem); button.ContextMenu=menu; PortalCards.Children.Add(button);
  }
 }
 private void PortalSearch_Changed(object sender,TextChangedEventArgs e){if(PortalCards is not null)BuildPortal();}
 private void RestoreHiddenServices_Click(object sender,RoutedEventArgs e){if(!_adminUnlocked)return;_policy.HiddenServiceIds.Clear();SavePolicy();BuildPortal();MessageBox.Show("כל השירותים שהוסתרו הוחזרו לפורטל.");}

 private async Task InitializeBrowserAsync() {
  try {
   string profile=Path.Combine(DataFolder,"WebView2"); Directory.CreateDirectory(profile);
   var environment=await CoreWebView2Environment.CreateAsync(null,profile);
   await Browser.EnsureCoreWebView2Async(environment);
   Browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled=true;
   Browser.CoreWebView2.Settings.AreDevToolsEnabled=false;
   Browser.CoreWebView2.Settings.IsStatusBarEnabled=false;
   Browser.CoreWebView2.NavigationStarting+=(_,e)=>{
    if(Uri.TryCreate(e.Uri,UriKind.Absolute,out var uri) && (uri.Scheme.Equals("ravkav",StringComparison.OrdinalIgnoreCase)||uri.Scheme.Equals("ravkavonline",StringComparison.OrdinalIgnoreCase))) {
     e.Cancel=true;
     if(Browser.Source?.Host.Equals("ravkavonline.co.il",StringComparison.OrdinalIgnoreCase)==true) {
      try { Process.Start(new ProcessStartInfo(e.Uri){UseShellExecute=true}); Log("פרוטוקול קורא רב־קו הופעל"); }
      catch(Exception ex){Log("הפעלת קורא רב־קו נכשלה",ex);MessageBox.Show("לא ניתן להפעיל את רכיב קורא הכרטיסים. בדוק שהרכיב הרשמי מותקן.");}
     } else Log("ניסיון להפעיל פרוטוקול חיצוני ממקור לא מורשה נחסם");
     return;
    }
    if(!Uri.TryCreate(e.Uri,UriKind.Absolute,out var target)||!IsAllowed(target)){e.Cancel=true;Log("ניווט נחסם: "+e.Uri);StatusText.Text="הניווט נחסם לפי מדיניות";}
   };
   Browser.CoreWebView2.NewWindowRequested+=(_,e)=>{
    e.Handled=true;
    if(_policy.AllowSameOriginPopups&&Uri.TryCreate(e.Uri,UriKind.Absolute,out var u)&&Browser.Source is not null&&u.Scheme=="https"&&u.Host.Equals(Browser.Source.Host,StringComparison.OrdinalIgnoreCase)&&IsAllowed(u)){
     Browser.CoreWebView2.Navigate(u.ToString()); Log("חלון קופץ מאותו דומיין אושר");
    } else Log("חלון קופץ נחסם: "+e.Uri);
   };
   Browser.CoreWebView2.WebMessageReceived+=(_,e)=>{
    if(!_elementPicking)return;
    try {
     using var data=JsonDocument.Parse(e.WebMessageAsJson);
     string selector=data.RootElement.GetProperty("selector").GetString()??"";
     if(selector.Length==0||selector.Length>2000)return;
     var selectedRule=new ElementRule{Host=Browser.Source?.Host??"",Selector=selector,Allowed=false}; _policy.Elements.Add(selectedRule);
     _elementPicking=false; _=ApplyElementRulesAsync(); ShowManagement("ניהול רכיבי עמוד"); SavePolicy();
    } catch(Exception ex){Log("בחירת רכיב נכשלה",ex);}
   };
   Browser.CoreWebView2.DOMContentLoaded+=async (_,_)=>{await ApplyElementRulesAsync();await ApplyPaymentRestrictionsAsync();};
   Browser.CoreWebView2.Navigate("https://hopon.co.il/read"); StatusText.Text="הדפדפן המאובטח מוכן";
  } catch(Exception ex){Log("אתחול WebView2 נכשל",ex);StatusText.Text="WebView2 לא זמין — בדוק התקנה";}
 }
 private bool IsAllowed(Uri uri) => PolicyEngine.IsAllowed(uri,_policy);
 private static string? NormalizePath(string value) {
  try {
   string path=value;
   for(int i=0;i<10&&path.Contains('%');i++){string decoded=Uri.UnescapeDataString(path);if(decoded==path)break;path=decoded;}
   if(path.Contains('%'))return null;
   path=path.Replace('\\','/');
   var parts=new List<string>();
   foreach(var part in path.Split('/')){if(part.Length==0||part==".")continue;if(part==".."){if(parts.Count>0)parts.RemoveAt(parts.Count-1);continue;}if(part.Any(char.IsControl))return null;parts.Add(part);}
   return "/"+string.Join("/",parts);
  } catch{return null;}
 }
 private static bool PathMatches(string actual,string root) {
  string normalized=NormalizePath(root)??"/";
  return normalized=="/"||actual.Equals(normalized,StringComparison.OrdinalIgnoreCase)||actual.StartsWith(normalized.TrimEnd('/')+"/",StringComparison.OrdinalIgnoreCase);
 }
 private void OpenService(PortalService service) {
  ShowBrowser(); AddressBox.Text=service.Url;
  if(Browser.CoreWebView2 is not null&&Uri.TryCreate(service.Url,UriKind.Absolute,out var uri)&&IsAllowed(uri))Browser.CoreWebView2.Navigate(service.Url);
  else StatusText.Text="הכתובת אינה מורשית או שהדפדפן עדיין נטען";
 }
 private void ShowBrowser(){DashboardView.Visibility=Visibility.Collapsed;ManagementView.Visibility=Visibility.Collapsed;BrowserView.Visibility=Visibility.Visible;PageTitle.Text="דפדפן פנימי";PageSubtitle.Text="גלישה בתוך התוכנה בהתאם למדיניות ההרשאות";}
 private void ShowManagement(string title){DashboardView.Visibility=Visibility.Collapsed;BrowserView.Visibility=Visibility.Collapsed;ManagementView.Visibility=Visibility.Visible;PageTitle.Text=title;PageSubtitle.Text="ניהול מקומי — השינויים נשמרים במחשב זה";RefreshGrids();}
 private void ShowDashboard(){DashboardView.Visibility=Visibility.Visible;BrowserView.Visibility=Visibility.Collapsed;ManagementView.Visibility=Visibility.Collapsed;PageTitle.Text="לוח בקרה";PageSubtitle.Text="כל השירותים וכלי הניהול במקום אחד";}
 private void RefreshGrids(){_rules.Clear();foreach(var r in _policy.Routes)_rules.Add(r);_elements.Clear();foreach(var e in _policy.Elements)_elements.Add(e);}
 private void RefreshCounts(){RulesCount.Text=_policy.Routes.Count.ToString();ServicesCount.Text=Services.Length.ToString();EventsCount.Text=_events.Count.ToString();}
 private void Log(string message,Exception? ex=null){_events.Insert(0,$"[{DateTime.Now:HH:mm:ss}] {message} {ex?.Message}");if(_events.Count>500)_events.RemoveAt(_events.Count-1);LogStatic(message,ex);RefreshCounts();}
 private async Task ApplyPaymentRestrictionsAsync() {
  if(Browser.CoreWebView2 is null)return;
  const string script=@"(()=>{const isGooglePay=e=>((e.innerText||'')+' '+(e.getAttribute('aria-label')||'')+' '+(e.getAttribute('title')||'')).toLowerCase().includes('google pay');document.querySelectorAll('button,a,[role=button],iframe').forEach(e=>{if(isGooglePay(e)||(e.tagName==='IFRAME'&&(e.src||'').toLowerCase().includes('pay.google.com'))){e.remove();}});})();";
  await Browser.CoreWebView2.ExecuteScriptAsync(script);
 }

 private async Task ApplyElementRulesAsync(){
  if(Browser.CoreWebView2 is null)return;
  var rules=_policy.Elements.Where(x=>x.Host.Equals(Browser.Source?.Host,StringComparison.OrdinalIgnoreCase)).ToArray();
  var payload=rules.Select(x=>new {
   selector=x.Selector, allowed=x.Allowed, text=x.ReplacementText,
   image=(!string.IsNullOrWhiteSpace(x.ReplacementImagePath)&&File.Exists(x.ReplacementImagePath))
    ? "data:"+(Path.GetExtension(x.ReplacementImagePath).ToLowerInvariant() is ".jpg" or ".jpeg" ? "image/jpeg" : "image/png")+";base64,"+Convert.ToBase64String(File.ReadAllBytes(x.ReplacementImagePath))
    : "",
   fit=x.ReplacementImageFit=="cover"?"cover":"contain"
  }).ToArray();
  string json=JsonSerializer.Serialize(payload);
  string script="""
  (() => {
    const rules = __DAAT_RULES__;
    const states = new WeakMap();
    let applying = false;
    let queued = false;
    const restore = (element, state) => {
      element.style.display = state.display;
      if (state.ariaHidden === null) element.removeAttribute('aria-hidden');
      else element.setAttribute('aria-hidden', state.ariaHidden);
      if (state.children) element.replaceChildren(...state.children.map(node => node.cloneNode(true)));
    };
    const apply = () => {
      if (applying || !document.documentElement) return;
      applying = true;
      try {
        for (let index = 0; index < rules.length; index++) {
          const rule = rules[index];
          let matches;
          try { matches = document.querySelectorAll(rule.selector); }
          catch (error) { console.warn('[DaatAharon] Invalid element selector', rule.selector, error); continue; }
          for (const element of matches) {
            const state = states.get(element);
            if (rule.allowed) {
              if (state) { restore(element, state); states.delete(element); }
              continue;
            }
            const key = String(index) + '|' + String(rule.text || '') + '|' + String(rule.image || '');
            if (state && state.key === key) continue;
            const original = state || {
              display: element.style.display,
              ariaHidden: element.getAttribute('aria-hidden'),
              children: [...element.childNodes].map(node => node.cloneNode(true))
            };
            original.key = key;
            states.set(element, original);
            if (rule.image) {
              const image = document.createElement('img');
              image.src = rule.image;
              image.alt = '';
              image.style.maxWidth = '100%';
              image.style.maxHeight = '100%';
              image.style.objectFit = rule.fit === 'cover' ? 'cover' : 'contain';
              element.replaceChildren(image);
              element.style.removeProperty('display');
              element.setAttribute('aria-hidden', 'false');
            } else if (rule.text) {
              element.textContent = rule.text;
              element.style.removeProperty('display');
              element.setAttribute('aria-hidden', 'false');
            } else {
              element.style.setProperty('display', 'none', 'important');
              element.setAttribute('aria-hidden', 'true');
            }
          }
        }
      } finally { applying = false; }
    };
    const schedule = () => {
      if (queued) return;
      queued = true;
      queueMicrotask(() => { queued = false; apply(); });
    };
    if (window.__daatObserver) window.__daatObserver.disconnect();
    window.__daatApplyElementRules = apply;
    apply();
    window.__daatObserver = new MutationObserver(schedule);
    window.__daatObserver.observe(document.documentElement, { childList: true, subtree: true });
  })();
  """.Replace("__DAAT_RULES__",json);
  try { await Browser.CoreWebView2.ExecuteScriptAsync(script); }
  catch(Exception ex) { Log("החלת כללי רכיבים נכשלה",ex); }
 }
 private async void PickElement_Click(object sender,RoutedEventArgs e){
  if(Browser.CoreWebView2 is null)return; _elementPicking=true;
  await Browser.CoreWebView2.ExecuteScriptAsync(@"(()=>{if(window.__daatPicker)return;window.__daatPicker=true;let last=null;document.addEventListener('mouseover',e=>{if(last)last.style.outline='';last=e.target;last.style.outline='3px solid #ffb347'},{capture:true});document.addEventListener('contextmenu',e=>{e.preventDefault();const path=[];let n=e.target;while(n&&n.nodeType===1&&path.length<8){let p=n.tagName.toLowerCase();if(n.id){p+='#'+CSS.escape(n.id);path.unshift(p);break;}const siblings=[...n.parentElement.children].filter(x=>x.tagName===n.tagName);if(siblings.length>1)p+=':nth-of-type('+(siblings.indexOf(n)+1)+')';path.unshift(p);n=n.parentElement;}chrome.webview.postMessage({selector:path.join(' > ')})},{capture:true})})()");
  StatusText.Text="בחר רכיב באתר בלחיצה ימנית";
 }

 private void ToggleTheme_Click(object sender,RoutedEventArgs e) {
  if(!_adminUnlocked)return;
  _policy.Theme=_policy.Theme=="midnight"?"light":"midnight"; ApplyTheme(); SavePolicy();
 }
 private void ApplyTheme() {
  bool dark=_policy.Theme!="light";
  SetBrush("WindowBrush",dark?System.Windows.Media.Color.FromRgb(11,16,32):System.Windows.Media.Color.FromRgb(239,243,249));
  SetBrush("PanelBrush",dark?System.Windows.Media.Color.FromRgb(18,26,43):System.Windows.Media.Color.FromRgb(255,255,255));
  SetBrush("TextBrush",dark?System.Windows.Media.Color.FromRgb(243,246,252):System.Windows.Media.Color.FromRgb(25,35,52));
  SetBrush("MutedBrush",dark?System.Windows.Media.Color.FromRgb(145,160,184):System.Windows.Media.Color.FromRgb(91,106,128));
  if(string.IsNullOrWhiteSpace(_policy.BackgroundImagePath)||!File.Exists(_policy.BackgroundImagePath))Background=(System.Windows.Media.Brush)Application.Current.Resources["WindowBrush"];
 }
 private static void SetBrush(string key,System.Windows.Media.Color color) {
  if(Application.Current.Resources[key] is System.Windows.Media.SolidColorBrush brush)brush.Color=color;
  else Application.Current.Resources[key]=new System.Windows.Media.SolidColorBrush(color);
 }
 private void ChooseBackground_Click(object sender,RoutedEventArgs e) {
  if(!_adminUnlocked)return;
  var dialog=new OpenFileDialog{Filter="תמונות|*.png;*.jpg;*.jpeg;*.bmp;*.gif"};
  if(dialog.ShowDialog()!=true)return;
  try {
   var folder=Path.Combine(DataFolder,"Assets");Directory.CreateDirectory(folder);
   var ext=Path.GetExtension(dialog.FileName).ToLowerInvariant();
   if(new FileInfo(dialog.FileName).Length>12*1024*1024)throw new InvalidDataException("תמונת הרקע גדולה מדי (מקסימום 12MB).");
   var target=Path.Combine(folder,"background"+ext);File.Copy(dialog.FileName,target,true);
   _policy.BackgroundImagePath=target;SavePolicy();ApplyBackground();
  } catch(Exception ex){MessageBox.Show("לא ניתן להחיל את תמונת הרקע: "+ex.Message);}
 }
 private void ApplyBackground() {
  try {
   if(string.IsNullOrWhiteSpace(_policy.BackgroundImagePath)||!File.Exists(_policy.BackgroundImagePath))return;
   var image=new System.Windows.Media.Imaging.BitmapImage();
   image.BeginInit();image.CacheOption=System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;image.UriSource=new Uri(_policy.BackgroundImagePath);image.EndInit();image.Freeze();
   Background=new System.Windows.Media.ImageBrush(image){Stretch=System.Windows.Media.Stretch.UniformToFill};
  } catch(Exception ex){Log("טעינת תמונת רקע נכשלה",ex);}
 }

 private void Window_PreviewKeyDown(object sender,KeyEventArgs e) {
  if(e.Key==Key.A && Keyboard.Modifiers==(ModifierKeys.Control|ModifierKeys.Shift)) { e.Handled=true; UnlockAdmin(); }
 }
 private void UnlockAdmin() {
  if(!AuthenticateAdmin())return;
  _adminUnlocked=true; AdminNav.Visibility=Visibility.Visible; StatusText.Text="מצב מנהל מאובטח פעיל"; Log("מנהל התחבר");
  ShowManagement("ניהול מערכת");
 }
 private bool AuthenticateAdmin() {
  if(!File.Exists(PasswordPath)&&File.Exists(LegacyPasswordPath)){Directory.CreateDirectory(DataFolder);File.Copy(LegacyPasswordPath,PasswordPath,true);Log("סיסמת מנהל הועברה מהגרסה הישנה");}
  if(!File.Exists(PasswordPath)) {
   var first=PasswordPrompt("הגדרת סיסמת מנהל חדשה"); if(first is null)return false;
   var second=PasswordPrompt("אימות סיסמת מנהל — הקלד שוב"); if(second is null)return false;
   if(first!=second || first.Length<8) { MessageBox.Show("הסיסמאות אינן תואמות או קצרות מדי (נדרשים לפחות 8 תווים)."); return false; }
   SavePassword(first); return true;
  }
  var entered=PasswordPrompt("כניסה לאזור הניהול"); if(entered is null)return false;
  try {
   using var doc=JsonDocument.Parse(File.ReadAllText(PasswordPath));
   var salt=Convert.FromBase64String(doc.RootElement.GetProperty("salt").GetString()!);
   var expected=Convert.FromBase64String(doc.RootElement.GetProperty("hash").GetString()!);
   var actual=Rfc2898DeriveBytes.Pbkdf2(entered,salt,PasswordIterations,HashAlgorithmName.SHA256,32);
   if(CryptographicOperations.FixedTimeEquals(expected,actual))return true;
  } catch(Exception ex){Log("אימות סיסמת מנהל נכשל",ex);}
  MessageBox.Show("הסיסמה שגויה.","אימות מנהל",MessageBoxButton.OK,MessageBoxImage.Warning); return false;
 }
 private string? PasswordPrompt(string title) {
  var dialog=new Window{Owner=this,Title=title,Width=390,Height=190,WindowStartupLocation=WindowStartupLocation.CenterOwner,ResizeMode=ResizeMode.NoResize,FlowDirection=FlowDirection.RightToLeft,Background=System.Windows.Media.Brushes.White};
  var panel=new StackPanel{Margin=new Thickness(18)};
  panel.Children.Add(new TextBlock{Text=title,FontSize=16,Margin=new Thickness(0,0,0,12)});
  var box=new PasswordBox{Padding=new Thickness(8),Margin=new Thickness(0,0,0,12)}; panel.Children.Add(box);
  var ok=new Button{Content="אישור",Padding=new Thickness(14,7,14,7),IsDefault=true,HorizontalAlignment=HorizontalAlignment.Left}; panel.Children.Add(ok);
  ok.Click+=(_,_)=>dialog.DialogResult=true; dialog.Content=panel;
  return dialog.ShowDialog()==true?box.Password:null;
 }
 private static void SavePassword(string password) {
  Directory.CreateDirectory(DataFolder); var salt=RandomNumberGenerator.GetBytes(16);
  var hash=Rfc2898DeriveBytes.Pbkdf2(password,salt,PasswordIterations,HashAlgorithmName.SHA256,32);
  File.WriteAllText(PasswordPath,JsonSerializer.Serialize(new{salt=Convert.ToBase64String(salt),hash=Convert.ToBase64String(hash),iterations=PasswordIterations}));
 }
 private void ChangeAdminPassword_Click(object sender,RoutedEventArgs e) {
  if(!_adminUnlocked)return;
  var old=PasswordPrompt("אימות הסיסמה הנוכחית"); if(old is null)return;
  try {
   using var doc=JsonDocument.Parse(File.ReadAllText(PasswordPath));
   var salt=Convert.FromBase64String(doc.RootElement.GetProperty("salt").GetString()!);
   var expected=Convert.FromBase64String(doc.RootElement.GetProperty("hash").GetString()!);
   var actual=Rfc2898DeriveBytes.Pbkdf2(old,salt,PasswordIterations,HashAlgorithmName.SHA256,32);
   if(!CryptographicOperations.FixedTimeEquals(expected,actual)){MessageBox.Show("הסיסמה הנוכחית שגויה.");return;}
  } catch { MessageBox.Show("לא ניתן לאמת את הסיסמה.");return; }
  var next=PasswordPrompt("הגדרת סיסמה חדשה"); if(next is null)return;
  var confirm=PasswordPrompt("אימות הסיסמה החדשה");
  if(confirm!=next||next.Length<8){MessageBox.Show("הסיסמאות אינן תואמות או קצרות מדי.");return;}
  SavePassword(next); MessageBox.Show("סיסמת המנהל עודכנה.");
 }
 private void AllowSelectedElement_Click(object sender,RoutedEventArgs e) {
  if(!_adminUnlocked||ElementsGrid.SelectedItem is not ElementRule item)return;
  item.Allowed=true; SavePolicy(); RefreshGrids(); if(Browser.CoreWebView2 is not null)Browser.CoreWebView2.Reload();
 }
 private void BlockSelectedElement_Click(object sender,RoutedEventArgs e) {
  if(!_adminUnlocked||ElementsGrid.SelectedItem is not ElementRule item)return;
  item.Allowed=false; SavePolicy(); RefreshGrids(); if(Browser.CoreWebView2 is not null)Browser.CoreWebView2.Reload();
 }
 private void EditSelectedElement_Click(object sender,RoutedEventArgs e) {
  if(!_adminUnlocked||ElementsGrid.SelectedItem is not ElementRule item)return;
  string replacement=Microsoft.VisualBasic.Interaction.InputBox("טקסט חלופי לרכיב (השאר ריק כדי להסתיר אותו)","עריכת רכיב",item.ReplacementText);
  item.ReplacementText=replacement; item.ReplacementImagePath=""; item.Allowed=false;
  SavePolicy(); RefreshGrids(); if(Browser.CoreWebView2 is not null)Browser.CoreWebView2.Reload();
 }
 private void PopupSettingChanged(object sender,RoutedEventArgs e) {
  if(!_adminUnlocked||AllowPopupsCheck is null)return;
  _policy.AllowSameOriginPopups=AllowPopupsCheck.IsChecked==true; SavePolicy();
 }

 private void Dashboard_Click(object s,RoutedEventArgs e)=>ShowDashboard();
 private void Portal_Click(object s,RoutedEventArgs e)=>ShowDashboard();
 private void Browser_Click(object s,RoutedEventArgs e)=>ShowBrowser();
 private void Policies_Click(object s,RoutedEventArgs e){if(_adminUnlocked)ShowManagement("חסימות והרשאות");}
 private void Popups_Click(object s,RoutedEventArgs e){if(_adminUnlocked)ShowManagement("ניהול חלונות קופצים");}
 private void Elements_Click(object s,RoutedEventArgs e){if(_adminUnlocked)ShowManagement("ניהול רכיבי עמוד");}
 private void Settings_Click(object s,RoutedEventArgs e){if(_adminUnlocked)ShowManagement("הגדרות וגיבוי");}
 private void Diagnostics_Click(object s,RoutedEventArgs e){if(_adminUnlocked)ShowManagement("יומן ואבחון");}
 private void Back_Click(object s,RoutedEventArgs e){if(Browser.CoreWebView2?.CanGoBack==true)Browser.CoreWebView2.GoBack();}
 private void NavigateAddress_Click(object s,RoutedEventArgs e){if(Uri.TryCreate(AddressBox.Text,UriKind.Absolute,out var uri)&&IsAllowed(uri))Browser.CoreWebView2?.Navigate(uri.ToString());else StatusText.Text="הכתובת חסומה לפי מדיניות";}
 private void AddBlockRule_Click(object s,RoutedEventArgs e){
  if(!_adminUnlocked)return;
  string host=Microsoft.VisualBasic.Interaction.InputBox("דומיין לחסימה (למשל example.com)","הוספת חסימה");
  if(string.IsNullOrWhiteSpace(host))return;
  host=host.Trim().TrimEnd('.').ToLowerInvariant();
  if(Uri.CheckHostName(host)==UriHostNameType.Unknown){MessageBox.Show("שם הדומיין אינו תקין.");return;}
  string path=Microsoft.VisualBasic.Interaction.InputBox("נתיב לחסימה (או / לכל הדומיין)","הוספת חסימה","/");
  if(string.IsNullOrWhiteSpace(path))path="/";
  if(!path.StartsWith('/'))path="/"+path;
  if(path.Length>2048||path.Any(char.IsControl)){MessageBox.Show("הנתיב אינו תקין.");return;}
  _policy.Routes.Add(new(){Host=host,Path=path,Allowed=false}); SavePolicy(); RefreshGrids();
 }
 private void AddAllowPath_Click(object s,RoutedEventArgs e){
  if(!_adminUnlocked)return;
  string host=Microsoft.VisualBasic.Interaction.InputBox("דומיין שבו לאשר נתיב","הרשאת נתיב");
  if(string.IsNullOrWhiteSpace(host))return;
  host=host.Trim().TrimEnd('.').ToLowerInvariant();
  if(Uri.CheckHostName(host)==UriHostNameType.Unknown){MessageBox.Show("שם הדומיין אינו תקין.");return;}
  string path=Microsoft.VisualBasic.Interaction.InputBox("הנתיב המדויק או תיקיית משנה","הרשאת נתיב","/");
  if(string.IsNullOrWhiteSpace(path))return;
  if(!path.StartsWith('/'))path="/"+path;
  if(path.Length>2048||path.Any(char.IsControl)){MessageBox.Show("הנתיב אינו תקין.");return;}
  _policy.Routes.Add(new(){Host=host,Path=path,Allowed=true}); SavePolicy(); RefreshGrids();
 }
 private void AddAllowHost_Click(object s,RoutedEventArgs e){string host=Microsoft.VisualBasic.Interaction.InputBox("דומיין מורשה","הוספת הרשאה");if(string.IsNullOrWhiteSpace(host))return;_policy.AllowedHosts.Add(host.Trim().ToLowerInvariant());_policy.AllowedHosts=_policy.AllowedHosts.Distinct(StringComparer.OrdinalIgnoreCase).ToList();SavePolicy();RefreshGrids();}
 private void RemoveRule_Click(object s,RoutedEventArgs e){if(RulesGrid.SelectedItem is RouteRule r)_policy.Routes.Remove(r);else if(ElementsGrid.SelectedItem is ElementRule el)_policy.Elements.Remove(el);SavePolicy();RefreshGrids();}
 private void SavePolicy_Click(object s,RoutedEventArgs e){SavePolicy();StatusText.Text="ההגדרות נשמרו בהצלחה";}
 private void ExportBackup_Click(object s,RoutedEventArgs e){var d=new SaveFileDialog{Filter="Daat Aharon backup (*.json)|*.json",FileName="DaatAharon-backup.json"};if(d.ShowDialog()==true)File.WriteAllText(d.FileName,JsonSerializer.Serialize(_policy,JsonOptions));}
 private void ImportBackup_Click(object s,RoutedEventArgs e){var d=new OpenFileDialog{Filter="Daat Aharon backup (*.json)|*.json"};if(d.ShowDialog()==true){try{_policy=JsonSerializer.Deserialize<LocalPolicy>(File.ReadAllText(d.FileName),JsonOptions)??new();SavePolicy();RefreshGrids();}catch(Exception ex){MessageBox.Show("קובץ הגיבוי אינו תקין: "+ex.Message);}}}
}
using System;
using System.Diagnostics;
using System.Drawing;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AhuCampus
{
    public sealed class MainForm : Form
    {
        readonly PortalClient client;
        readonly CredentialStore credentialStore;
        readonly Color ink=Color.FromArgb(27,42,65);
        readonly Color muted=Color.FromArgb(103,116,132);
        readonly Color teal=Color.FromArgb(0,112,108);
        TextBox account, password, log;
        Button connect,retry,cancel;
        Label status,detail;
        CheckBox show,remember;
        LinkLabel clearSaved;
        CancellationTokenSource pending;
        bool changingRemember;
        bool retryAvailable;

        public MainForm(PortalClient client) : this(client,CredentialStore.CreateDefault()) { }

        public MainForm(PortalClient client,CredentialStore credentialStore)
        {
            this.client=client;
            this.credentialStore=credentialStore;
            Text="安徽大学 · 校园网助手";
            Name="MainForm";
            Font=new Font("Microsoft YaHei UI",10F);
            BackColor=Color.FromArgb(245,247,250);
            ForeColor=ink;
            AutoScaleDimensions=new SizeF(96F,96F);
            AutoScaleMode=AutoScaleMode.Dpi;
            ClientSize=new Size(560,650);
            MinimumSize=new Size(576,689);
            MaximumSize=new Size(760,900);
            StartPosition=FormStartPosition.CenterScreen;
            Icon=LoadApplicationIcon();

            var root=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Padding=new Padding(24),BackColor=BackColor};
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,99));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,246));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,93));
            root.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            Controls.Add(root);

            var header=new Panel {Dock=DockStyle.Fill};
            var tag=Label("AHU  /  CAMPUS NETWORK",9,FontStyle.Bold);
            tag.ForeColor=teal; tag.SetBounds(0,0,450,20);
            var title=Label("校园网，一键连接",23,FontStyle.Bold); title.SetBounds(0,24,490,44);
            var caption=Label("安徽大学  ·  先连接校园 Wi-Fi 或插入网线",10,FontStyle.Regular);
            caption.ForeColor=muted; caption.SetBounds(1,71,495,25);
            header.Controls.AddRange(new Control[]{tag,title,caption}); root.Controls.Add(header,0,0);

            var card=new TableLayoutPanel {Dock=DockStyle.Fill,BackColor=Color.White,Padding=new Padding(18,12,18,12),ColumnCount=1,RowCount=6,Margin=new Padding(0,8,0,8)};
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            foreach (int height in new[]{25,37,25,37,29,49}) card.RowStyles.Add(new RowStyle(SizeType.Absolute,height));
            card.Controls.Add(Label("账号",10,FontStyle.Bold),0,0);
            account=new TextBox {Name="Account",AccessibleName="校园网账号",Dock=DockStyle.Fill,Font=new Font(Font.FontFamily,12),MaxLength=128,Margin=new Padding(0,1,0,3)};
            card.Controls.Add(account,0,1);
            card.Controls.Add(Label("密码",10,FontStyle.Bold),0,2);
            password=new TextBox {Name="Password",AccessibleName="校园网密码",UseSystemPasswordChar=true,Dock=DockStyle.Fill,Font=new Font(Font.FontFamily,12),MaxLength=256,Margin=new Padding(0,1,0,3)};
            card.Controls.Add(password,0,3);
            var credentialOptions=new FlowLayoutPanel {Dock=DockStyle.Fill,AutoSize=false,FlowDirection=FlowDirection.LeftToRight,WrapContents=false,Margin=new Padding(0)};
            show=new CheckBox {Name="ShowPassword",Text="显示密码",AutoSize=true,Margin=new Padding(0,3,18,0),ForeColor=muted};
            show.CheckedChanged+=delegate {password.UseSystemPasswordChar=!show.Checked;};
            remember=new CheckBox {Name="RememberCredentials",Text="记住账号和密码",Checked=true,AutoSize=true,Margin=new Padding(0,3,18,0),ForeColor=muted};
            remember.CheckedChanged+=delegate {
                if(!remember.Checked && !changingRemember) ClearSavedCredentials(false);
            };
            clearSaved=new LinkLabel {Name="ClearCredentials",AccessibleName="清除已保存信息",Text="清除已保存信息",AutoSize=true,LinkColor=teal,Margin=new Padding(0,4,0,0)};
            clearSaved.LinkClicked+=delegate {ClearSavedCredentials(true);};
            credentialOptions.Controls.AddRange(new Control[]{show,remember,clearSaved});
            card.Controls.Add(credentialOptions,0,4);

            var buttons=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Margin=new Padding(0,5,0,0)};
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,54));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,21));
            connect=Button("连接","Connect",true);
            retry=Button("重试","Retry",false);
            retry.Enabled=false;
            cancel=Button("取消","Cancel",false); cancel.Enabled=false;
            connect.Click+=async delegate {await ConnectAsync(false);};
            retry.Click+=async delegate {await ConnectAsync(true);};
            cancel.Click+=delegate {if(pending!=null) pending.Cancel();};
            account.TextChanged+=delegate {CredentialsEdited();};
            password.TextChanged+=delegate {CredentialsEdited();};
            buttons.Controls.Add(connect,0,0); buttons.Controls.Add(retry,1,0); buttons.Controls.Add(cancel,2,0);
            card.Controls.Add(buttons,0,5); root.Controls.Add(card,0,1);
            AcceptButton=connect;

            var stateBox=new Panel {Dock=DockStyle.Fill,BackColor=Color.FromArgb(231,240,242),Padding=new Padding(14),Margin=new Padding(0,3,0,10)};
            status=Label("等待连接",12,FontStyle.Bold); status.Name="Status"; status.Dock=DockStyle.Top; status.Height=29;
            detail=Label("输入校园网账号和密码，点击「连接」。",9,FontStyle.Regular); detail.Name="Detail"; detail.Dock=DockStyle.Fill; detail.ForeColor=muted;
            stateBox.Controls.Add(detail); stateBox.Controls.Add(status); root.Controls.Add(stateBox,0,2);

            var bottom=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Margin=new Padding(0)};
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            bottom.RowStyles.Add(new RowStyle(SizeType.Absolute,25));
            bottom.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            bottom.RowStyles.Add(new RowStyle(SizeType.Absolute,29));
            bottom.RowStyles.Add(new RowStyle(SizeType.Absolute,22));
            bottom.Controls.Add(Label("连接记录",9,FontStyle.Bold),0,0);
            log=new TextBox {Name="Log",AccessibleName="连接记录",Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Fill,BackColor=Color.White,BorderStyle=BorderStyle.FixedSingle,Font=new Font(Font.FontFamily,9),Margin=new Padding(0)};
            bottom.Controls.Add(log,0,1);
            var link=new LinkLabel {Text="打开学校网页登录",AutoSize=true,LinkColor=teal,Margin=new Padding(0,6,0,0)};
            link.LinkClicked+=delegate {try {Process.Start(new ProcessStartInfo("http://172.16.253.3/a79.htm"){UseShellExecute=true});} catch {SetStatus("无法打开浏览器","请手动访问 172.16.253.3。",false);}};
            bottom.Controls.Add(link,0,2);
            var note=Label("勾选“记住”后，凭据由 Windows 为当前用户加密保存。",8.5F,FontStyle.Regular); note.ForeColor=muted;
            bottom.Controls.Add(note,0,3); root.Controls.Add(bottom,0,3);
            FormClosing+=delegate {if(pending!=null) pending.Cancel(); password.Clear();};
            LoadSavedCredentials();
            Shown+=delegate {if(account.TextLength==0) account.Focus(); else connect.Focus();};
        }

        Label Label(string text,float size,FontStyle style)
        {
            return new Label {Text=text,Font=new Font("Microsoft YaHei UI",size,style),ForeColor=ink,AutoSize=false,Dock=DockStyle.None,Size=new Size(480,24),Margin=new Padding(0)};
        }
        static Icon LoadApplicationIcon()
        {
            using(var embedded=Icon.ExtractAssociatedIcon(Application.ExecutablePath))
                return embedded==null ? (Icon)SystemIcons.Application.Clone() : (Icon)embedded.Clone();
        }
        Button Button(string text,string name,bool primary)
        {
            return new Button {Text=text,Name=name,AccessibleName=text,Dock=DockStyle.Fill,FlatStyle=FlatStyle.Flat,
                BackColor=primary?teal:Color.FromArgb(241,245,247),ForeColor=primary?Color.White:ink,
                Font=new Font(Font.FontFamily,10,FontStyle.Bold),Cursor=Cursors.Hand,Margin=new Padding(0,0,8,0)};
        }
        async Task ConnectAsync(bool isRetry)
        {
            if(pending!=null) return;
            if(isRetry && !retryAvailable) return;
            string user=account.Text.Trim();
            string secret=password.Text;
            if(String.IsNullOrWhiteSpace(user) || String.IsNullOrEmpty(secret))
            {
                retryAvailable=false;
                ApplyIdleButtons();
                SetStatus("请填写账号和密码","使用与学校网页登录相同的账号和密码。",false);
                if(user.Length==0) account.Focus(); else password.Focus();
                return;
            }
            var operation=new CancellationTokenSource();
            pending=operation;
            if(remember.Checked)
            {
                try {credentialStore.Save(user,secret);}
                catch(Exception e)
                {
                    if(!(e is System.Security.Cryptography.CryptographicException) && !(e is System.IO.IOException) && !(e is UnauthorizedAccessException)) throw;
                    AddLog("无法保存账号密码，本次仍继续连接。");
                }
            }
            SetBusy(true,isRetry);
            SetStatus(isRetry?"正在重试…":"正在连接…",isRetry?"正在重新获取当前网络信息。":"正在获取当前网络信息。",false);
            AddLog(isRetry?"开始重试。":"开始连接。");
            bool allowRetry=true;
            var progress=new Progress<string>(message=>{
                if(!IsDisposed && !Disposing && pending==operation)
                {
                    string safe=PortalProtocol.Redact(message,user,secret);
                    detail.Text=safe;
                    AddLog(safe);
                }
            });
            try
            {
                var result=await client.LoginAsync(user,secret,progress,operation.Token);
                if(IsDisposed || Disposing) return;
                allowRetry=!result.InternetVerified;
                SetStatus(result.InternetVerified?"已连接":result.Accepted?"认证已接受 · 待确认外网":"连接未成功",result.Message,result.InternetVerified);
                AddLog(result.Message);
            }
            catch(OperationCanceledException)
            {
                if(!IsDisposed && !Disposing)
                {
                    string reason=operation.IsCancellationRequested?"已取消等待；如果认证已发出，可用浏览器确认当前网络状态。":"请求超时，请检查校园网连接后重试。";
                    SetStatus(operation.IsCancellationRequested?"已取消":"连接超时",reason,false); AddLog(reason);
                }
            }
            catch(InvalidOperationException e) {ShowFailure(PortalProtocol.Redact(e.Message,user,secret));}
            catch(HttpRequestException) {ShowFailure("无法访问认证服务器，请检查校园网连接后重试。");}
            catch(Exception) {ShowFailure("连接遇到错误，请重试或使用学校网页登录。");}
            finally
            {
                if(pending==operation) pending=null;
                operation.Dispose();
                if(!IsDisposed && !Disposing)
                {
                    retryAvailable=allowRetry;
                    SetBusy(false,false);
                }
            }
        }
        void SetBusy(bool busy,bool retrying)
        {
            account.Enabled=password.Enabled=show.Enabled=remember.Enabled=clearSaved.Enabled=!busy;
            cancel.Enabled=busy;
            connect.Text=busy && !retrying?"连接中…":"连接";
            retry.Text=busy && retrying?"重试中…":"重试";
            if(busy)
            {
                connect.Enabled=retry.Enabled=false;
            }
            else ApplyIdleButtons();
        }
        void ApplyIdleButtons()
        {
            connect.Enabled=!retryAvailable;
            retry.Enabled=retryAvailable;
            AcceptButton=retryAvailable?retry:connect;
        }
        void CredentialsEdited()
        {
            if(pending!=null || !retryAvailable) return;
            retryAvailable=false;
            ApplyIdleButtons();
        }
        void SetStatus(string title,string message,bool success)
        {
            status.Text=title; status.ForeColor=success?teal:ink; detail.Text=message;
        }
        void ShowFailure(string message)
        {
            if(IsDisposed || Disposing) return;
            SetStatus("连接未成功",message,false); AddLog(message);
        }
        void AddLog(string message)
        {
            if(log.TextLength>12000) log.Clear();
            log.AppendText(DateTime.Now.ToString("HH:mm:ss")+"  "+message+Environment.NewLine);
        }
        void LoadSavedCredentials()
        {
            SavedCredentials saved=null;
            try {saved=credentialStore.Load();}
            catch(Exception e)
            {
                if(!(e is System.Security.Cryptography.CryptographicException) && !(e is System.IO.IOException) && !(e is UnauthorizedAccessException)) throw;
            }
            if(saved==null) return;
            account.Text=saved.Account;
            password.Text=saved.Password;
            remember.Checked=true;
            detail.Text="已加载保存的账号密码，可以直接连接。";
        }
        void ClearSavedCredentials(bool clearFields)
        {
            try {credentialStore.Clear();}
            catch(System.IO.IOException) {AddLog("无法删除已保存信息，请关闭其他占用程序后重试。"); return;}
            catch(UnauthorizedAccessException) {AddLog("无法删除已保存信息，请检查文件权限。"); return;}
            if(remember.Checked)
            {
                changingRemember=true;
                try {remember.Checked=false;}
                finally {changingRemember=false;}
            }
            if(clearFields) {account.Clear(); password.Clear(); account.Focus();}
            SetStatus("已清除保存信息",clearFields?"账号和密码已从本机删除。":"关闭程序后将不再自动填充。",false);
            AddLog("已清除保存的账号密码。");
        }
    }
}

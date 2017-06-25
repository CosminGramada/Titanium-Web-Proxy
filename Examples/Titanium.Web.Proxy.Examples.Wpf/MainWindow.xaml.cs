﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Helpers;
using Titanium.Web.Proxy.Models;

namespace Titanium.Web.Proxy.Examples.Wpf
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ProxyServer proxyServer;

        private int lastSessionNumber;

        public ObservableCollection<SessionListItem> Sessions { get; } =  new ObservableCollection<SessionListItem>();

        public SessionListItem SelectedSession
        {
            get { return selectedSession; }
            set
            {
                if (value != selectedSession)
                {
                    selectedSession = value;
                    SelectedSessionChanged();
                }
            }
        }

        private readonly Dictionary<SessionEventArgs, SessionListItem> sessionDictionary = new Dictionary<SessionEventArgs, SessionListItem>();
        private SessionListItem selectedSession;

        public MainWindow()
        {
            proxyServer = new ProxyServer();
            proxyServer.TrustRootCertificate = true;
            proxyServer.ForwardToUpstreamGateway = true;

            var explicitEndPoint = new ExplicitProxyEndPoint(IPAddress.Any, 8000, true);
            proxyServer.AddEndPoint(explicitEndPoint);
            proxyServer.BeforeRequest += ProxyServer_BeforeRequest;
            proxyServer.BeforeResponse += ProxyServer_BeforeResponse;
            proxyServer.TunnelConnectRequest += ProxyServer_TunnelConnectRequest;
            proxyServer.TunnelConnectResponse += ProxyServer_TunnelConnectResponse;
            proxyServer.Start();

            proxyServer.SetAsSystemProxy(explicitEndPoint, ProxyProtocolType.AllHttp);

            InitializeComponent();
        }

        private async Task ProxyServer_TunnelConnectRequest(object sender, TunnelConnectSessionEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                AddSession(e);
            });
        }

        private async Task ProxyServer_TunnelConnectResponse(object sender, SessionEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                SessionListItem item;
                if (sessionDictionary.TryGetValue(e, out item))
                {
                    item.Update();
                }
            });
        }

        private async Task ProxyServer_BeforeRequest(object sender, SessionEventArgs e)
        {
            SessionListItem item = null;
            Dispatcher.Invoke(() =>
            {
                item = AddSession(e);
            });

            if (e.WebSession.Request.HasBody)
            {
                item.RequestBody = await e.GetRequestBody();
            }
        }

        private async Task ProxyServer_BeforeResponse(object sender, SessionEventArgs e)
        {
            SessionListItem item = null;
            Dispatcher.Invoke(() =>
            {
                SessionListItem item2;
                if (sessionDictionary.TryGetValue(e, out item2))
                {
                    item2.Response.ResponseStatusCode = e.WebSession.Response.ResponseStatusCode;
                    item2.Response.ResponseStatusDescription = e.WebSession.Response.ResponseStatusDescription;
                    item2.Response.HttpVersion = e.WebSession.Response.HttpVersion;
                    item2.Response.ResponseHeaders.AddHeaders(e.WebSession.Response.ResponseHeaders);
                    item2.Update();
                    item = item2;
                }
            });

            if (item != null)
            {
                item.ResponseBody = await e.GetResponseBody();
            }
        }

        private SessionListItem AddSession(SessionEventArgs e)
        {
            var item = CreateSessionListItem(e);
            Sessions.Add(item);
            sessionDictionary.Add(e, item);
            return item;
        }

        private SessionListItem CreateSessionListItem(SessionEventArgs e)
        {
            lastSessionNumber++;
            var item = new SessionListItem
            {
                Number = lastSessionNumber,
                SessionArgs = e,
                Request =
                {
                    Method = e.WebSession.Request.Method,
                    RequestUri = e.WebSession.Request.RequestUri,
                    HttpVersion = e.WebSession.Request.HttpVersion,
                },
            };

            item.Request.RequestHeaders.AddHeaders(e.WebSession.Request.RequestHeaders);

            if (e is TunnelConnectSessionEventArgs)
            {
                e.DataReceived += (sender, args) =>
                {
                    var session = (SessionEventArgs)sender;
                    SessionListItem li;
                    if (sessionDictionary.TryGetValue(session, out li))
                    {
                        li.ReceivedDataCount += args.Count;
                    }
                };

                e.DataSent += (sender, args) =>
                {
                    var session = (SessionEventArgs)sender;
                    SessionListItem li;
                    if (sessionDictionary.TryGetValue(session, out li))
                    {
                        li.SentDataCount += args.Count;
                    }
                };
            }

            item.Update();
            return item;
        }

        private void ListViewSessions_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                var selectedItems = ((ListView)sender).SelectedItems;
                foreach (var item in selectedItems.Cast<SessionListItem>().ToArray())
                {
                    Sessions.Remove(item);
                    sessionDictionary.Remove(item.SessionArgs);
                }
            }
        }

        private void SelectedSessionChanged()
        {
            if (SelectedSession == null)
            {
                return;
            }

            var session = SelectedSession;
            var data = session.RequestBody ?? new byte[0];
            data = data.Take(1024).ToArray();

            string dataStr = string.Join(" ", data.Select(x => x.ToString("X2")));
            TextBoxRequest.Text = session.Request.HeaderText + dataStr;

            data = session.ResponseBody ?? new byte[0];
            data = data.Take(1024).ToArray();

            dataStr = string.Join(" ", data.Select(x => x.ToString("X2")));
            TextBoxResponse.Text = session.Response.HeaderText + dataStr;
        }
    }
}
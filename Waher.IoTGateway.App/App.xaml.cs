﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Waher.Content;
using Waher.Events;
using Waher.Networking.XMPP;
using Waher.Networking.XMPP.Provisioning;
using Waher.Networking.XMPP.ServiceDiscovery;
using Waher.Persistence;
using Waher.Persistence.Files;
using Waher.Runtime.Inventory;
using Waher.Runtime.Settings;
using Waher.Things;
using Waher.Things.Gpio;

namespace Waher.IoTGateway.App
{
	/// <summary>
	/// Provides application-specific behavior to supplement the default Application class.
	/// </summary>
	sealed partial class App : Application
	{
		/// <summary>
		/// Initializes the singleton application object.  This is the first line of authored code
		/// executed, and as such is the logical equivalent of main() or WinMain().
		/// </summary>
		public App()
		{
			this.InitializeComponent();
			this.Suspending += OnSuspending;
		}

		/// <summary>
		/// Invoked when the application is launched normally by the end user.  Other entry points
		/// will be used such as when the application is launched to open a specific file.
		/// </summary>
		/// <param name="e">Details about the launch request and process.</param>
		protected override void OnLaunched(LaunchActivatedEventArgs e)
		{
			Frame rootFrame = Window.Current.Content as Frame;

			// Do not repeat app initialization when the Window already has content,
			// just ensure that the window is active
			if (rootFrame == null)
			{
				// Create a Frame to act as the navigation context and navigate to the first page
				rootFrame = new Frame();

				rootFrame.NavigationFailed += OnNavigationFailed;

				if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
				{
					//TODO: Load state from previously suspended application
				}

				// Place the frame in the current Window
				Window.Current.Content = rootFrame;
			}

			if (e.PrelaunchActivated == false)
			{
				if (rootFrame.Content == null)
				{
					// When the navigation stack isn't restored navigate to the first page,
					// configuring the new page by passing required information as a navigation
					// parameter
					rootFrame.Navigate(typeof(MainPage), e.Arguments);
				}
				// Ensure the current window is active
				Window.Current.Activate();
				Task.Run((Action)this.Init);
			}
		}

		private async void Init()
		{
			try
			{
				Log.RegisterExceptionToUnnest(typeof(System.Runtime.InteropServices.ExternalException));
				Log.RegisterExceptionToUnnest(typeof(System.Security.Authentication.AuthenticationException));

				AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
				{
					if (e.IsTerminating)
					{
						using (StreamWriter w = File.CreateText(Path.Combine(Gateway.AppDataFolder, "UnhandledException.txt")))
						{
							w.Write("Type: ");

							if (e.ExceptionObject != null)
								w.WriteLine(e.ExceptionObject.GetType().FullName);
							else
								w.WriteLine("null");

							w.Write("Time: ");
							w.WriteLine(DateTime.Now.ToString());

							w.WriteLine();
							if (e.ExceptionObject is Exception ex)
							{
								w.WriteLine(ex.Message);
								w.WriteLine();
								w.WriteLine(ex.StackTrace);
							}
							else
							{
								if (e.ExceptionObject != null)
									w.WriteLine(e.ExceptionObject.ToString());

								w.WriteLine();
								w.WriteLine(Environment.StackTrace);
							}

							w.Flush();
						}
					}

					if (e.ExceptionObject is Exception ex2)
						Log.Critical(ex2);
					else if (e.ExceptionObject != null)
						Log.Critical(e.ExceptionObject.ToString());
					else
						Log.Critical("Unexpected null exception thrown.");
				};

				Gateway.GetDatabaseProvider += GetDatabase;
				Gateway.GetXmppClientCredentials += GetXmppClientCredentials;
				Gateway.XmppCredentialsUpdated += XmppCredentialsUpdated;
				Gateway.RegistrationSuccessful += RegistrationSuccessful;
				Gateway.GetMetaData += GetMetaData;

				if (!await Gateway.Start(false))
					throw new Exception("Gateway being started in another process.");
			}
			catch (Exception ex)
			{
				Log.Emergency(ex);

				MessageDialog Dialog = new MessageDialog(ex.Message, "Error");
				await MainPage.Instance.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
					async () => await Dialog.ShowAsync());
			}
		}

		private static Task<IDatabaseProvider> GetDatabase(XmlElement DatabaseConfig)
		{
			if (CommonTypes.TryParse(DatabaseConfig.Attributes["encrypted"].Value, out bool Encrypted) && Encrypted)
				throw new Exception("Encrypted database storage not supported on this platform.");

			return Task.FromResult<IDatabaseProvider>(new FilesProvider(Gateway.AppDataFolder + DatabaseConfig.Attributes["folder"].Value,
				DatabaseConfig.Attributes["defaultCollectionName"].Value,
				int.Parse(DatabaseConfig.Attributes["blockSize"].Value),
				int.Parse(DatabaseConfig.Attributes["blocksInCache"].Value),
				int.Parse(DatabaseConfig.Attributes["blobBlockSize"].Value), Encoding.UTF8,
				int.Parse(DatabaseConfig.Attributes["timeoutMs"].Value),
				false));
		}

		private static async Task<XmppCredentials> GetXmppClientCredentials(string XmppConfigFileName)
		{
			string Host = await RuntimeSettings.GetAsync("XmppHost", "waher.se");
			int Port = (int)await RuntimeSettings.GetAsync("XmppPort", 5222);
			string UserName = await RuntimeSettings.GetAsync("XmppUserName", string.Empty);
			string PasswordHash = await RuntimeSettings.GetAsync("XmppPasswordHash", string.Empty);
			string PasswordHashMethod = await RuntimeSettings.GetAsync("XmppPasswordHashMethod", string.Empty);
			string ThingRegistry = await RuntimeSettings.GetAsync("XmppRegistry", string.Empty);
			string Provisioning = await RuntimeSettings.GetAsync("XmppProvisioning", string.Empty);
			string Key = string.Empty;
			string Secret = string.Empty;
			bool TrustCertificate = await RuntimeSettings.GetAsync("XmppTrustCertificate", false);
			bool AllowInsecure = await RuntimeSettings.GetAsync("XmppAllowInsecure", false);
			bool StorePassword = await RuntimeSettings.GetAsync("XmppStorePassword", false);
			bool CreateAccount = false;

			if (string.IsNullOrEmpty(Host) ||
				Port <= 0 || Port > ushort.MaxValue ||
				string.IsNullOrEmpty(UserName) ||
				string.IsNullOrEmpty(PasswordHash) ||
				string.IsNullOrEmpty(PasswordHashMethod))
			{
				XmppClient Client = null;

				try
				{
					while (true)
					{
						TaskCompletionSource<bool> Search = new TaskCompletionSource<bool>();

						await MainPage.Instance.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
						{
							try
							{
								AccountDialog Dialog = new AccountDialog(Host, Port, UserName, TrustCertificate, AllowInsecure, StorePassword, CreateAccount, Key, Secret);

								switch (await Dialog.ShowAsync())
								{
									case ContentDialogResult.Primary:
										Host = Dialog.Host;
										Port = Dialog.Port;
										UserName = Dialog.UserName;
										PasswordHash = Dialog.Password;
										PasswordHashMethod = string.Empty;
										TrustCertificate = Dialog.TrustServer;
										AllowInsecure = Dialog.AllowInsecure;
										StorePassword = Dialog.AllowStorePassword;
										CreateAccount = Dialog.AllowRegistration;
										Key = Dialog.Key;
										Secret = Dialog.Secret;

										if (Client != null)
										{
											Client.Dispose();
											Client = null;
										}

										Client = new XmppClient(Host, Port, UserName, PasswordHash, "en", typeof(App).Assembly)
										{
											TrustServer = TrustCertificate,
											AllowCramMD5 = AllowInsecure,
											AllowDigestMD5 = AllowInsecure,
											AllowPlain = AllowInsecure,
											AllowScramSHA1 = true,
											AllowEncryption = true
										};

										if (CreateAccount)
											Client.AllowRegistration(Key, Secret);

										Client.OnStateChanged += (sender, State) =>
										{
											if (State == XmppState.Connected)
											{
												if (!StorePassword)
												{
													PasswordHash = Client.PasswordHash;
													PasswordHashMethod = Client.PasswordHashMethod;
												}

												Client.SendServiceItemsDiscoveryRequest(Client.Domain, (sender2, e2) =>
												{
													if (e2.Ok)
													{
														Dictionary<string, bool> Items = new Dictionary<string, bool>();

														foreach (Item Item in e2.Items)
															Items[Item.JID] = true;

														foreach (Item Item in e2.Items)
														{
															Log.Informational("Checking " + Item.JID + ".");

															Client.SendServiceDiscoveryRequest(Item.JID, (sender3, e3) =>
															{
																string JID = (string)e3.State;

																if (e3.Ok)
																{
																	if (e3.Features.ContainsKey(ThingRegistryClient.NamespaceDiscovery))
																	{
																		ThingRegistry = JID;
																		Console.Out.WriteLine("Thing registry found.", ThingRegistry);
																	}

																	if (e3.Features.ContainsKey(ProvisioningClient.NamespaceProvisioningDevice))
																	{
																		Provisioning = JID;
																		Console.Out.WriteLine("Provisioning server found.", Provisioning);
																	}
																}

																Items.Remove(JID);
																if (Items.Count == 0)
																	Search.SetResult(true);

															}, Item.JID);
														}
													}
													else
														Search.SetResult(true);
												}, null);
											}
										};

										Client.OnConnectionError += (sender, e) =>
										{
											Search.SetResult(false);
										};

										Log.Informational("Connecting to " + Host + ":" + Port.ToString());
										Client.Connect();
										break;

									case ContentDialogResult.Secondary:
									default:
										Search.SetResult(false);
										break;
								}
							}
							catch (Exception ex)
							{
								Log.Critical(ex);
							}
						});

						if (await Search.Task)
						{
							await RuntimeSettings.SetAsync("XmppHost", Host);
							await RuntimeSettings.SetAsync("XmppPort", Port);
							await RuntimeSettings.SetAsync("XmppUserName", UserName);
							await RuntimeSettings.SetAsync("XmppPasswordHash", PasswordHash);
							await RuntimeSettings.SetAsync("XmppPasswordHashMethod", PasswordHashMethod);
							await RuntimeSettings.SetAsync("XmppTrustCertificate", TrustCertificate);
							await RuntimeSettings.SetAsync("XmppAllowInsecure", AllowInsecure);
							await RuntimeSettings.SetAsync("XmppStorePassword", StorePassword);
							await RuntimeSettings.SetAsync("XmppRegistry", ThingRegistry);
							await RuntimeSettings.SetAsync("XmppProvisioning", Provisioning);

							return new XmppCredentials()
							{
								Host = Host,
								Port = Port,
								Account = UserName,
								Password = PasswordHash,
								PasswordType = PasswordHashMethod,
								TrustServer = TrustCertificate,
								AllowCramMD5 = AllowInsecure,
								AllowDigestMD5 = AllowInsecure,
								AllowPlain = AllowInsecure,
								AllowScramSHA1 = true,
								AllowEncryption = true,
								AllowRegistration = false,
								FormSignatureKey = string.Empty,
								FormSignatureSecret = string.Empty,
								ThingRegistry = ThingRegistry,
								Provisioning = Provisioning,
								Events = string.Empty
							};
						}
					}
				}
				finally
				{
					if (Client != null)
					{
						Client.Dispose();
						Client = null;
					}
				}
			}
			else
			{
				return new XmppCredentials()
				{
					Host = Host,
					Port = Port,
					Account = UserName,
					Password = PasswordHash,
					PasswordType = PasswordHashMethod,
					TrustServer = TrustCertificate,
					AllowCramMD5 = AllowInsecure,
					AllowDigestMD5 = AllowInsecure,
					AllowPlain = AllowInsecure,
					AllowScramSHA1 = true,
					AllowEncryption = true,
					AllowRegistration = false,
					FormSignatureKey = string.Empty,
					FormSignatureSecret = string.Empty,
					ThingRegistry = ThingRegistry,
					Provisioning = Provisioning,
					Events = string.Empty
				};
			}

		}

		private static async Task XmppCredentialsUpdated(string XmppConfigFileName, XmppCredentials Credentials)
		{
			bool StorePassword = await RuntimeSettings.GetAsync("XmppStorePassword", false);

			if (!StorePassword)
			{
				await RuntimeSettings.SetAsync("XmppPasswordHash", Credentials.Password);
				await RuntimeSettings.SetAsync("XmppPasswordHashMethod", Credentials.PasswordType);
			}
		}

		private async Task<MetaDataTag[]> GetMetaData(MetaDataTag[] MetaData)
		{
			List<MetaDataTag> Result = new List<MetaDataTag>(MetaData);
			string s;

			if (await RuntimeSettings.GetAsync("ThingRegistry.Location", false))
			{
				s = await RuntimeSettings.GetAsync("ThingRegistry.Country", string.Empty);
				if (!string.IsNullOrEmpty(s))
					Result.Add(new MetaDataStringTag("COUNTRY", s));

				s = await RuntimeSettings.GetAsync("ThingRegistry.Region", string.Empty);
				if (!string.IsNullOrEmpty(s))
					Result.Add(new MetaDataStringTag("REGION", s));

				s = await RuntimeSettings.GetAsync("ThingRegistry.City", string.Empty);
				if (!string.IsNullOrEmpty(s))
					Result.Add(new MetaDataStringTag("CITY", s));

				s = await RuntimeSettings.GetAsync("ThingRegistry.Area", string.Empty);
				if (!string.IsNullOrEmpty(s))
					Result.Add(new MetaDataStringTag("AREA", s));

				s = await RuntimeSettings.GetAsync("ThingRegistry.Street", string.Empty);
				if (!string.IsNullOrEmpty(s))
					Result.Add(new MetaDataStringTag("STREET", s));

				s = await RuntimeSettings.GetAsync("ThingRegistry.StreetNr", string.Empty);
				if (!string.IsNullOrEmpty(s))
					Result.Add(new MetaDataStringTag("STREETNR", s));

				s = await RuntimeSettings.GetAsync("ThingRegistry.Building", string.Empty);
				if (!string.IsNullOrEmpty(s))
					Result.Add(new MetaDataStringTag("BLD", s));

				s = await RuntimeSettings.GetAsync("ThingRegistry.Apartment", string.Empty);
				if (!string.IsNullOrEmpty(s))
					Result.Add(new MetaDataStringTag("APT", s));

				s = await RuntimeSettings.GetAsync("ThingRegistry.Room", string.Empty);
				if (!string.IsNullOrEmpty(s))
					Result.Add(new MetaDataStringTag("ROOM", s));

				s = await RuntimeSettings.GetAsync("ThingRegistry.Name", string.Empty);
				if (!string.IsNullOrEmpty(s))
					Result.Add(new MetaDataStringTag("NAME", s));
			}
			else
			{
				TaskCompletionSource<bool> UserInput = new TaskCompletionSource<bool>();

				await MainPage.Instance.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
				{
					try
					{
						RegistrationDialog Dialog = new RegistrationDialog();

						switch (await Dialog.ShowAsync())
						{
							case ContentDialogResult.Primary:
								await RuntimeSettings.SetAsync("ThingRegistry.Country", s = Dialog.Reg_Country);
								if (!string.IsNullOrEmpty(s))
									Result.Add(new MetaDataStringTag("COUNTRY", s));

								await RuntimeSettings.SetAsync("ThingRegistry.Region", s = Dialog.Reg_Region);
								if (!string.IsNullOrEmpty(s))
									Result.Add(new MetaDataStringTag("REGION", s));

								await RuntimeSettings.SetAsync("ThingRegistry.City", s = Dialog.Reg_City);
								if (!string.IsNullOrEmpty(s))
									Result.Add(new MetaDataStringTag("CITY", s));

								await RuntimeSettings.SetAsync("ThingRegistry.Area", s = Dialog.Reg_Area);
								if (!string.IsNullOrEmpty(s))
									Result.Add(new MetaDataStringTag("AREA", s));

								await RuntimeSettings.SetAsync("ThingRegistry.Street", s = Dialog.Reg_Street);
								if (!string.IsNullOrEmpty(s))
									Result.Add(new MetaDataStringTag("STREET", s));

								await RuntimeSettings.SetAsync("ThingRegistry.StreetNr", s = Dialog.Reg_StreetNr);
								if (!string.IsNullOrEmpty(s))
									Result.Add(new MetaDataStringTag("STREETNR", s));

								await RuntimeSettings.SetAsync("ThingRegistry.Building", s = Dialog.Reg_Building);
								if (!string.IsNullOrEmpty(s))
									Result.Add(new MetaDataStringTag("BLD", s));

								await RuntimeSettings.SetAsync("ThingRegistry.Apartment", s = Dialog.Reg_Apartment);
								if (!string.IsNullOrEmpty(s))
									Result.Add(new MetaDataStringTag("APT", s));

								await RuntimeSettings.SetAsync("ThingRegistry.Room", s = Dialog.Reg_Room);
								if (!string.IsNullOrEmpty(s))
									Result.Add(new MetaDataStringTag("ROOM", s));

								await RuntimeSettings.SetAsync("ThingRegistry.Name", s = Dialog.Name);
								if (!string.IsNullOrEmpty(s))
									Result.Add(new MetaDataStringTag("NAME", s));

								await RuntimeSettings.SetAsync("ThingRegistry.Location", true);

								UserInput.SetResult(true);
								break;

							case ContentDialogResult.Secondary:
								UserInput.SetResult(false);
								break;
						}
					}
					catch (Exception ex)
					{
						Log.Critical(ex);
					}
				});

				await UserInput.Task;
			}

			return Result.ToArray();
		}

		private static async Task RegistrationSuccessful(MetaDataTag[] MetaData, RegistrationEventArgs e)
		{
			if (!e.IsClaimed && Types.TryGetModuleParameter("Registry", out object Obj) && Obj is ThingRegistryClient ThingRegistryClient)
			{
				string ClaimUrl = ThingRegistryClient.EncodeAsIoTDiscoURI(MetaData);
				string FilePath = Path.Combine(Gateway.AppDataFolder, "Gateway.iotdisco");

				Log.Informational("Registration successful.");
				Log.Informational(ClaimUrl, new KeyValuePair<string, object>("Path", FilePath));

				await File.WriteAllTextAsync(FilePath, ClaimUrl);
			}
		}

		/// <summary>
		/// Invoked when Navigation to a certain page fails
		/// </summary>
		/// <param name="sender">The Frame which failed navigation</param>
		/// <param name="e">Details about the navigation failure</param>
		void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
		{
			throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
		}

		/// <summary>
		/// Invoked when application execution is being suspended.  Application state is saved
		/// without knowing whether the application will be terminated or resumed with the contents
		/// of memory still intact.
		/// </summary>
		/// <param name="sender">The source of the suspend request.</param>
		/// <param name="e">Details about the suspend request.</param>
		private void OnSuspending(object sender, SuspendingEventArgs e)
		{
			var deferral = e.SuspendingOperation.GetDeferral();

			Gateway.Stop();
			Log.Terminate();

			deferral.Complete();
		}
	}
}

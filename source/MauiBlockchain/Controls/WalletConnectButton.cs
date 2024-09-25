using System.Diagnostics;
using System.Transactions;
using System.Windows.Input;
using Nethereum.Signer;
using Nethereum.Util;
using WalletConnectSharp.Auth.Internals;
using WalletConnectSharp.Common.Utils;
using WalletConnectSharp.Core;
using WalletConnectSharp.Network.Models;
using WalletConnectSharp.Sign;
using WalletConnectSharp.Sign.Models;
using WalletConnectSharp.Sign.Models.Engine;
using WalletConnectSharp.Storage;

namespace MauiBlockchain
{
    public class WalletConnectButton : Button
    {
        private const string WALLET_CONNECT_PROJECT_ID = "PROJECT_ID_HERE";
        private const string CHAIN_ID = "eip155:1";

        public static readonly BindableProperty SignedInCommandProperty = BindableProperty.Create(
            nameof(SignedInCommand),
            typeof(ICommand),
            typeof(WalletConnectButton),
            defaultValue: null);

        public ICommand SignedInCommand
        {
            get { return (ICommand)GetValue(SignedInCommandProperty); }
            set { SetValue(SignedInCommandProperty, value); }
        }

        public static readonly BindableProperty ErrorCommandProperty = BindableProperty.Create(
            nameof(ErrorCommand),
            typeof(ICommand),
            typeof(WalletConnectButton),
            defaultValue: null);

        public ICommand ErrorCommand
        {
            get { return (ICommand)GetValue(ErrorCommandProperty); }
            set { SetValue(ErrorCommandProperty, value); }
        }
        
        private WalletConnectSignClient _dappClient;
        
        public WalletConnectButton()
        {
            Text = "Sign in with WalletConnect";

            Command = new Command(OnClicked);
        }

        private async void OnClicked()
        {
            //Connects the app with the wallet.
            var connected = await ConnectWallet();
            if (!connected)
            {
                return;
            }
            
            //User's wallet address
            var address = _dappClient.AddressProvider.DefaultSession.CurrentAddress(CHAIN_ID).Address;
                
            //Return the siweMessage and the signature
            var (signature, siweMessage) = await SignInWithEthereum(address);

            if (signature == null || siweMessage == null)
            {
                var errorMsg = "Error: An error occurred signing in with ethereum";
                Debug.WriteLine(errorMsg); 
                ErrorCommand?.Execute(errorMsg);
                return;
            }

            //Verifies that the address that signs the SIWE message is the user's address.
            var isValid = VerifyPersonalSignSignature(siweMessage.ToString(), signature, address);
            if (isValid)
            {
                Debug.WriteLine("Success: User authenticated with Ethereum");
                SignedInCommand?.Execute(new AutenticatedUserData
                {
                    AuthenticationNonce = siweMessage.Nonce,
                    UserAddress = address,
                    WalletName = _dappClient.AddressProvider.DefaultSession.Peer.Metadata.Name,
                    UserPublicKey = _dappClient.AddressProvider.DefaultSession.Peer.PublicKey
                });
                return;
            }
                
            ErrorCommand?.Execute("The wallet signature is not valid");
            Debug.WriteLine("Error: The wallet signature is not valid");
        }
        
        private async Task<bool> ConnectWallet()
        {
            //Modify this function to customize the client options. 
            var dappOptions = CreateSignClientOptions();

            //Modify this function to customize the connection options.
            var dappConnectOptions = CreateConnectionOptions();

            //Initiates the client
            _dappClient = await WalletConnectSignClient.Init(dappOptions);
            var connectData = await _dappClient.Connect(dappConnectOptions);

            //Launch the wallet app with the connection params
            //On android the OS will let you choose the wallet app but on iOS it won't
            var uri = new Uri(connectData.Uri);
            await Launcher.Default.OpenAsync(uri);
            
            try
            {
                //Wait until the user approves the connection
                //If the connection is not approved or it times out an exception will be thrown
                await connectData.Approval;
                
                Debug.WriteLine("Approved");
                return true;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
                ErrorCommand?.Execute(e.Message);
            }

            return false;
        }
        
        private async Task<(string, SiweMessage)> SignInWithEthereum(string address)
        {
            //Creates the SIWE message that will be signed by the user's wallet
            var siweMessage = CreateSiweMessage();

            //Create the request following the RPC format for 'personal_sign'
            var request = new EthPersonalSign(siweMessage.ToString(), address);

            //Launches the wallet app
            Application.Current.Dispatcher.Dispatch(async () => await Launcher.Default.OpenAsync($"wc:"));
            
            string signature;
            try
            {
                //Send the sign request to the user's wallet with the siwe message
                signature = await _dappClient.Request<EthPersonalSign, string>(_dappClient.AddressProvider.DefaultSession.Topic, request, CHAIN_ID, siweMessage.Expiry);
            }
            catch (Exception e)
            {
                signature = null;
                siweMessage = null;
            }
            
            return (signature, siweMessage);
        }
        
        public bool VerifyPersonalSignSignature(string message, string signature, string expectedAddress)
        {
            // Recover the address from the signature
            var signer = new EthereumMessageSigner();
            var recoveredAddress = signer.EncodeUTF8AndEcRecover(message, signature);

            // Compare the recovered address with the expected address
            return string.Equals(recoveredAddress, expectedAddress, StringComparison.OrdinalIgnoreCase);
        }

        private SiweMessage CreateSiweMessage()
        {
            return new SiweMessage
            {
                ChainId = CHAIN_ID,
                Aud = "https://uxdivers.com",
                Domain = "uxdivers.com",
                Nonce = CryptoUtils.GenerateNonce(),
                Statement = "Sign-In with Ethereum Example",
                Expiry = (long)new TimeSpan(0, 10, 0).TotalSeconds,
            };
        }

        private SignClientOptions CreateSignClientOptions()
        {
            return new SignClientOptions()
            {
                ProjectId = WALLET_CONNECT_PROJECT_ID,
                Metadata = new Metadata()
                {
                    Description = "MAUI WalletConnect and SIWE example",
                    Icons = new[]
                    {
                        "https://cdn.prod.website-files.com/630fae9a46ee72ef23481b76/643471939ca472a0f0fa96b6_favicon.png"
                    },
                    Name = "MAUI SIWE Example",
                    Url = "https://uxdivers.com/"
                },
                Storage = new InMemoryStorage()
            };
        }
        
        private ConnectOptions CreateConnectionOptions()
        {
            return new ConnectOptions()
            {
                Expiry = (long)new TimeSpan(0,10,0).TotalSeconds,
                RequiredNamespaces = new RequiredNamespaces()
                {
                    {
                        "eip155", new ProposedNamespace()
                        {
                            Methods = new[]
                            {
                                "eth_sendTransaction", "eth_signTransaction", "eth_sign", "personal_sign", "eth_signTypedData",
                            },
                            Chains = new[]
                            {
                                CHAIN_ID
                            },
                            Events = new[]
                            {
                                "chainChanged", "accountsChanged", "connect", "disconnect"
                            }
                        }
                    }
                }
            };
        }

        public class SiweMessage
        {
            public string Domain { get; set; }
            public string Address { get; set; }
            public string Statement { get; set; }
            public string Aud { get; set; }
            public string ChainId { get; set; }
            public string Nonce { get; set; }
            public long? Expiry { get; set; }
            public string Version => "1";
            public string Iat { get; } = DateTime.Now.ToISOString();

            public override string ToString()
            {
                var header = $"{Domain} wants you to sign in with your Ethereum account:";
                var walletAddress = Address + "\n" + (Statement != null ? "" : "\n");
                var statement = Statement + "\n";
                var uri = $"URI: {Aud}";
                var version = $"Version: {Version}";
                var chainId = $"Chain ID: {ChainId}";
                var nonce = $"Nonce: {Nonce}";
                var issuedAt = $"Issued At: {Iat}";

                var message = string.Join('\n',
                    new string[] { header, walletAddress, statement, uri, version, chainId, nonce, issuedAt }
                        .Where(val => !string.IsNullOrWhiteSpace(val)));

                return message;
            }
        }
        
        [RpcMethod("personal_sign"), RpcRequestOptions(Clock.ONE_MINUTE, 99998)]
        public class EthPersonalSign : List<string>
        {
            public EthPersonalSign(string message, string address) : base(new List<string> { message, address })
            {
                
            }

            public EthPersonalSign()
            {
                
            }
        }
    }
}

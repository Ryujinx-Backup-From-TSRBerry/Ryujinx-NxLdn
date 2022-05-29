using LibHac.Ns;
using Ryujinx.Common;
using Ryujinx.Common.Configuration.Multiplayer;
using Ryujinx.Common.Logging;
using Ryujinx.Common.Utilities;
using Ryujinx.Cpu;
using Ryujinx.HLE.HOS.Ipc;
using Ryujinx.HLE.HOS.Kernel.Threading;
using Ryujinx.HLE.HOS.Services.Ldn.Types;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.RyuLdn;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.RyuLdn.Types;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.Spacemeowx2Ldn;
using Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.NxLdn;
using Ryujinx.Memory;
using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator
{
    internal class IUserLocalCommunicationService : IpcService, IDisposable
    {
        public static string LanPlayHost = "ldn.ryujinx.org";

        public static short LanPlayPort = 30456;

        private const int NIFM_REQUEST_ID = 90;

        private const string DEFAULT_IP_ADDRESS = "127.0.0.1";

        private const string DEFAULT_SUBNET_MASK = "255.255.255.0";

        private const bool IS_DEVELOPMENT = false;

        private KEvent _stateChangeEvent;

        private NetworkState _state;

        private DisconnectReason _disconnectReason;

        private ResultCode _nifmResultCode;

        private ulong _currentPid;

        private AccessPoint _accessPoint;

        private Station _station;

        public INetworkClient NetworkClient { get; private set; }

        public IUserLocalCommunicationService(ServiceCtx context)
        {
            _stateChangeEvent = new KEvent(context.Device.System.KernelContext);
            _state = NetworkState.None;
            _disconnectReason = DisconnectReason.None;
        }

        private ushort CheckDevelopmentChannel(ushort channel)
        {
            return 0;
        }

        private SecurityMode CheckDevelopmentSecurityMode(SecurityMode securityMode)
        {
            return SecurityMode.Retail;
        }

        private bool CheckLocalCommunicationIdPermission(ServiceCtx context, ulong localCommunicationIdChecked)
        {
            ApplicationControlProperty controlProperty = context.Device.Application.ControlData.Value;
            return (controlProperty.LocalCommunicationId[0] == localCommunicationIdChecked || controlProperty.LocalCommunicationId[1] == localCommunicationIdChecked || controlProperty.LocalCommunicationId[2] == localCommunicationIdChecked || controlProperty.LocalCommunicationId[3] == localCommunicationIdChecked || controlProperty.LocalCommunicationId[4] == localCommunicationIdChecked || controlProperty.LocalCommunicationId[5] == localCommunicationIdChecked || controlProperty.LocalCommunicationId[6] == localCommunicationIdChecked) | (controlProperty.LocalCommunicationId[7] == localCommunicationIdChecked);
        }

        [CommandHipc(0)]
        public ResultCode GetState(ServiceCtx context)
        {
            if (_nifmResultCode != 0)
            {
                context.ResponseData.Write(6);
                return ResultCode.Success;
            }
            context.ResponseData.Write((int)_state);
            return ResultCode.Success;
        }

        public void SetState()
        {
            _stateChangeEvent.WritableEvent.Signal();
        }

        public void SetState(NetworkState state)
        {
            _state = state;
            SetState();
        }

        [CommandHipc(1)]
        public ResultCode GetNetworkInfo(ServiceCtx context)
        {
            ulong bufferPosition = context.Request.RecvListBuff[0].Position;
            MemoryHelper.FillWithZeros(context.Memory, bufferPosition, 1152);
            if (_nifmResultCode != 0)
            {
                return _nifmResultCode;
            }
            NetworkInfo networkInfo;
            ResultCode resultCode = GetNetworkInfoImpl(out networkInfo);
            if (resultCode != 0)
            {
                return resultCode;
            }
            ulong infoSize = MemoryHelper.Write(context.Memory, bufferPosition, networkInfo);
            context.Response.PtrBuff[0] = context.Response.PtrBuff[0].WithSize(infoSize);
            return ResultCode.Success;
        }

        private ResultCode GetNetworkInfoImpl(out NetworkInfo networkInfo)
        {
            if (_state == NetworkState.StationConnected)
            {
                networkInfo = _station.NetworkInfo;
            }
            else
            {
                if (_state != NetworkState.AccessPointCreated)
                {
                    networkInfo = default(NetworkInfo);
                    return ResultCode.InvalidState;
                }
                networkInfo = _accessPoint.NetworkInfo;
            }
            return ResultCode.Success;
        }

        private NodeLatestUpdate[] GetNodeLatestUpdateImpl(int count)
        {
            if (_state == NetworkState.StationConnected)
            {
                return _station.LatestUpdates.ConsumeLatestUpdate(count);
            }
            if (_state == NetworkState.AccessPointCreated)
            {
                return _accessPoint.LatestUpdates.ConsumeLatestUpdate(count);
            }
            return new NodeLatestUpdate[0];
        }

        [CommandHipc(2)]
        public ResultCode GetIpv4Address(ServiceCtx context)
        {
            if (_nifmResultCode != 0)
            {
                return _nifmResultCode;
            }
            if (_state == NetworkState.AccessPointCreated || _state == NetworkState.StationConnected)
            {
                ProxyConfig config = _state switch
                {
                    NetworkState.AccessPointCreated => _accessPoint.Config,
                    NetworkState.StationConnected => _station.Config,
                    _ => default(ProxyConfig),
                };
                if (config.ProxyIp == 0)
                {
                    UnicastIPAddressInformation unicastAddress = NetworkHelpers.GetLocalInterface(context.Device.Configuration.MultiplayerLanInterfaceId).Item2;
                    if (unicastAddress == null)
                    {
                        context.ResponseData.Write(NetworkHelpers.ConvertIpv4Address("127.0.0.1"));
                        context.ResponseData.Write(NetworkHelpers.ConvertIpv4Address("255.255.255.0"));
                    }
                    else
                    {
                        Logger.Info?.Print(LogClass.ServiceLdn, $"Console's LDN IP is \"{unicastAddress.Address}\".", "GetIpv4Address");
                        context.ResponseData.Write(NetworkHelpers.ConvertIpv4Address(unicastAddress.Address));
                        context.ResponseData.Write(NetworkHelpers.ConvertIpv4Address("255.255.255.0"));
                    }
                }
                else
                {
                    Logger.Info?.Print(LogClass.ServiceLdn, "LDN obtained proxy IP.", "GetIpv4Address");
                    context.ResponseData.Write(config.ProxyIp);
                    context.ResponseData.Write(config.ProxySubnetMask);
                }
                return ResultCode.Success;
            }
            return ResultCode.InvalidArgument;
        }

        [CommandHipc(3)]
        public ResultCode GetDisconnectReason(ServiceCtx context)
        {
            context.ResponseData.Write((short)_disconnectReason);
            return ResultCode.Success;
        }

        public void SetDisconnectReason(DisconnectReason reason)
        {
            if (_state != NetworkState.Initialized)
            {
                _disconnectReason = reason;
                SetState(NetworkState.Initialized);
            }
        }

        [CommandHipc(4)]
        public ResultCode GetSecurityParameter(ServiceCtx context)
        {
            if (_nifmResultCode != 0)
            {
                return _nifmResultCode;
            }
            NetworkInfo networkInfo;
            ResultCode resultCode = GetNetworkInfoImpl(out networkInfo);
            if (resultCode != 0)
            {
                return resultCode;
            }
            SecurityParameter securityParameter2 = default(SecurityParameter);
            securityParameter2.Data = new byte[16];
            securityParameter2.SessionId = networkInfo.NetworkId.SessionId;
            SecurityParameter securityParameter = securityParameter2;
            context.ResponseData.WriteStruct(securityParameter);
            return ResultCode.Success;
        }

        [CommandHipc(5)]
        public ResultCode GetNetworkConfig(ServiceCtx context)
        {
            if (_nifmResultCode != 0)
            {
                return _nifmResultCode;
            }
            NetworkInfo networkInfo;
            ResultCode resultCode = GetNetworkInfoImpl(out networkInfo);
            if (resultCode != 0)
            {
                return resultCode;
            }
            NetworkConfig networkConfig2 = default(NetworkConfig);
            networkConfig2.IntentId = networkInfo.NetworkId.IntentId;
            networkConfig2.Channel = networkInfo.Common.Channel;
            networkConfig2.NodeCountMax = networkInfo.Ldn.NodeCountMax;
            networkConfig2.LocalCommunicationVersion = networkInfo.Ldn.Nodes[0].LocalCommunicationVersion;
            networkConfig2.Reserved2 = new byte[10];
            NetworkConfig networkConfig = networkConfig2;
            context.ResponseData.WriteStruct(networkConfig);
            return ResultCode.Success;
        }

        [CommandHipc(100)]
        public ResultCode AttachStateChangeEvent(ServiceCtx context)
        {
            if (context.Process.HandleTable.GenerateHandle(_stateChangeEvent.ReadableEvent, out var stateChangeEventHandle) != 0)
            {
                throw new InvalidOperationException("Out of handles!");
            }
            context.Response.HandleDesc = IpcHandleDesc.MakeCopy(stateChangeEventHandle);
            return ResultCode.Success;
        }

        [CommandHipc(101)]
        public ResultCode GetNetworkInfoLatestUpdate(ServiceCtx context)
        {
            ulong bufferPosition = context.Request.RecvListBuff[0].Position;
            MemoryHelper.FillWithZeros(context.Memory, bufferPosition, 1152);
            if (_nifmResultCode != 0)
            {
                return _nifmResultCode;
            }
            NetworkInfo networkInfo;
            ResultCode resultCode = GetNetworkInfoImpl(out networkInfo);
            if (resultCode != 0)
            {
                return resultCode;
            }
            ulong outputPosition = context.Request.RecvListBuff[0].Position;
            ulong outputSize = context.Request.RecvListBuff[0].Size;
            ulong latestUpdateSize = (ulong)Marshal.SizeOf<NodeLatestUpdate>();
            int count = (int)(outputSize / latestUpdateSize);
            NodeLatestUpdate[] nodeLatestUpdateImpl = GetNodeLatestUpdateImpl(count);
            MemoryHelper.FillWithZeros(context.Memory, outputPosition, (int)outputSize);
            NodeLatestUpdate[] array = nodeLatestUpdateImpl;
            foreach (NodeLatestUpdate node in array)
            {
                MemoryHelper.Write(context.Memory, outputPosition, node);
                outputPosition += latestUpdateSize;
            }
            ulong infoSize = MemoryHelper.Write(context.Memory, bufferPosition, networkInfo);
            context.Response.PtrBuff[0] = context.Response.PtrBuff[0].WithSize(infoSize);
            return ResultCode.Success;
        }

        [CommandHipc(102)]
        public ResultCode Scan(ServiceCtx context)
        {
            return ScanImpl(context);
        }

        [CommandHipc(103)]
        public ResultCode ScanPrivate(ServiceCtx context)
        {
            return ScanImpl(context, isPrivate: true);
        }

        private ResultCode ScanImpl(ServiceCtx context, bool isPrivate = false)
        {
            ushort channel = (ushort)context.RequestData.ReadUInt64();
            ScanFilter scanFilter = context.RequestData.ReadStruct<ScanFilter>();
            var (bufferPosition, bufferSize) = context.Request.GetBufferType0x22();
            if (_nifmResultCode != 0)
            {
                return _nifmResultCode;
            }
            if (!isPrivate)
            {
                channel = CheckDevelopmentChannel(channel);
            }
            ResultCode resultCode = ResultCode.InvalidArgument;
            if (bufferSize != 0L && bufferPosition != 0L)
            {
                ScanFilterFlag scanFilterFlag = scanFilter.Flag;
                if (!scanFilterFlag.HasFlag(ScanFilterFlag.NetworkType) || scanFilter.NetworkType <= NetworkType.All)
                {
                    if (scanFilterFlag.HasFlag(ScanFilterFlag.Ssid) && scanFilter.Ssid.Length <= 31)
                    {
                        return resultCode;
                    }
                    if (scanFilterFlag.HasFlag(ScanFilterFlag.MacAddress))
                    {
                        throw new NotSupportedException();
                    }
                    if (scanFilterFlag > ScanFilterFlag.All)
                    {
                        return resultCode;
                    }
                    if (_state - 3 >= NetworkState.AccessPoint)
                    {
                        resultCode = ResultCode.InvalidState;
                    }
                    else
                    {
                        if (scanFilter.NetworkId.IntentId.LocalCommunicationId == -1)
                        {
                            ApplicationControlProperty controlProperty = context.Device.Application.ControlData.Value;
                            scanFilter.NetworkId.IntentId.LocalCommunicationId = (long)controlProperty.LocalCommunicationId[0];
                        }
                        resultCode = ScanInternal(context.Memory, channel, scanFilter, bufferPosition, bufferSize, out var counter);
                        context.ResponseData.Write(counter);
                    }
                }
            }
            return resultCode;
        }

        private ResultCode ScanInternal(IVirtualMemoryManager memory, ushort channel, ScanFilter scanFilter, ulong bufferPosition, ulong bufferSize, out ulong counter)
        {
            ulong networkInfoSize = (ulong)Marshal.SizeOf(typeof(NetworkInfo));
            ulong maxGames = bufferSize / networkInfoSize;
            MemoryHelper.FillWithZeros(memory, bufferPosition, (int)bufferSize);
            NetworkInfo[] array = NetworkClient.Scan(channel, scanFilter);
            counter = 0uL;
            NetworkInfo[] array2 = array;
            foreach (NetworkInfo networkInfo in array2)
            {
                MemoryHelper.Write(memory, bufferPosition + networkInfoSize * counter, networkInfo);
                if (++counter >= maxGames)
                {
                    break;
                }
            }
            return ResultCode.Success;
        }

        [CommandHipc(104)]
        public ResultCode SetWirelessControllerRestriction(ServiceCtx context)
        {
            if (context.RequestData.ReadUInt32() > 1)
            {
                return ResultCode.InvalidArgument;
            }
            if (_state != NetworkState.Initialized)
            {
                return ResultCode.InvalidState;
            }
            return ResultCode.Success;
        }

        [CommandHipc(200)]
        public ResultCode OpenAccessPoint(ServiceCtx context)
        {
            if (_nifmResultCode != 0)
            {
                return _nifmResultCode;
            }
            if (_state != NetworkState.Initialized)
            {
                return ResultCode.InvalidState;
            }
            _station?.Dispose();
            _station = null;
            SetState(NetworkState.AccessPoint);
            _accessPoint = new AccessPoint(this);
            return ResultCode.Success;
        }

        [CommandHipc(201)]
        public ResultCode CloseAccessPoint(ServiceCtx context)
        {
            if (_nifmResultCode != 0)
            {
                return _nifmResultCode;
            }
            if (_state == NetworkState.AccessPoint || _state == NetworkState.AccessPointCreated)
            {
                DestroyNetworkImpl(DisconnectReason.DestroyedByUser);
                SetState(NetworkState.Initialized);
                return ResultCode.Success;
            }
            return ResultCode.InvalidState;
        }

        private void CloseAccessPoint()
        {
            _accessPoint?.Dispose();
            _accessPoint = null;
        }

        [CommandHipc(202)]
        public ResultCode CreateNetwork(ServiceCtx context)
        {
            return CreateNetworkImpl(context);
        }

        [CommandHipc(203)]
        public ResultCode CreateNetworkPrivate(ServiceCtx context)
        {
            return CreateNetworkImpl(context, isPrivate: true);
        }

        public ResultCode CreateNetworkImpl(ServiceCtx context, bool isPrivate = false)
        {
            SecurityConfig securityConfig = context.RequestData.ReadStruct<SecurityConfig>();
            SecurityParameter securityParameter = (isPrivate ? context.RequestData.ReadStruct<SecurityParameter>() : default(SecurityParameter));
            UserConfig userConfig = context.RequestData.ReadStruct<UserConfig>();
            context.RequestData.ReadUInt32();
            NetworkConfig networkConfig = context.RequestData.ReadStruct<NetworkConfig>();
            if (networkConfig.IntentId.LocalCommunicationId == -1)
            {
                ApplicationControlProperty controlProperty = context.Device.Application.ControlData.Value;
                networkConfig.IntentId.LocalCommunicationId = (long)controlProperty.LocalCommunicationId[0];
            }
            if (!CheckLocalCommunicationIdPermission(context, (ulong)networkConfig.IntentId.LocalCommunicationId))
            {
                return ResultCode.InvalidObject;
            }
            if (_nifmResultCode != 0)
            {
                return _nifmResultCode;
            }
            networkConfig.Channel = CheckDevelopmentChannel(networkConfig.Channel);
            securityConfig.SecurityMode = CheckDevelopmentSecurityMode(securityConfig.SecurityMode);
            if (networkConfig.NodeCountMax <= 8 && ((ulong)networkConfig.LocalCommunicationVersion & 0x80000000uL) == 0L && (int)securityConfig.SecurityMode <= 1 && securityConfig.Passphrase.Length <= 64)
            {
                if (_state == NetworkState.AccessPoint)
                {
                    if (isPrivate)
                    {
                        ulong bufferPosition = context.Request.PtrBuff[0].Position;
                        byte[] addressListBytes = new byte[context.Request.PtrBuff[0].Size];
                        context.Memory.Read(bufferPosition, addressListBytes);
                        AddressList addressList = LdnHelper.FromBytes<AddressList>(addressListBytes);
                        _accessPoint.CreateNetworkPrivate(securityConfig, securityParameter, userConfig, networkConfig, addressList);
                    }
                    else
                    {
                        _accessPoint.CreateNetwork(securityConfig, userConfig, networkConfig);
                    }
                    return ResultCode.Success;
                }
                return ResultCode.InvalidState;
            }
            return ResultCode.InvalidArgument;
        }

        [CommandHipc(204)]
        public ResultCode DestroyNetwork(ServiceCtx context)
        {
            return DestroyNetworkImpl(DisconnectReason.DestroyedByUser);
        }

        private ResultCode DestroyNetworkImpl(DisconnectReason disconnectReason)
        {
            if (_nifmResultCode != 0)
            {
                return _nifmResultCode;
            }
            if (disconnectReason - 3 <= DisconnectReason.DisconnectedByUser)
            {
                if (_state == NetworkState.AccessPointCreated)
                {
                    CloseAccessPoint();
                    SetState(NetworkState.AccessPoint);
                    return ResultCode.Success;
                }
                CloseAccessPoint();
                return ResultCode.InvalidState;
            }
            return ResultCode.InvalidArgument;
        }

        [CommandHipc(205)]
        public ResultCode Reject(ServiceCtx context)
        {
            uint nodeId = context.RequestData.ReadUInt32();
            return RejectImpl(DisconnectReason.Rejected, nodeId);
        }

        private ResultCode RejectImpl(DisconnectReason disconnectReason, uint nodeId)
        {
            if (_nifmResultCode != 0)
            {
                return _nifmResultCode;
            }
            if (_state != NetworkState.AccessPointCreated)
            {
                return ResultCode.InvalidState;
            }
            return NetworkClient.Reject(disconnectReason, nodeId);
        }

        [CommandHipc(206)]
        public ResultCode SetAdvertiseData(ServiceCtx context)
        {
            var (bufferPosition, bufferSize) = context.Request.GetBufferType0x21();
            if (_nifmResultCode != 0)
            {
                return _nifmResultCode;
            }
            if (bufferSize == 0L || bufferSize > 384)
            {
                return ResultCode.InvalidArgument;
            }
            if (_state == NetworkState.AccessPoint || _state == NetworkState.AccessPointCreated)
            {
                byte[] advertiseData = new byte[bufferSize];
                context.Memory.Read(bufferPosition, advertiseData);
                return _accessPoint.SetAdvertiseData(advertiseData);
            }
            return ResultCode.InvalidState;
        }

        [CommandHipc(207)]
        public ResultCode SetStationAcceptPolicy(ServiceCtx context)
        {
            AcceptPolicy acceptPolicy = (AcceptPolicy)context.RequestData.ReadByte();
            if (_nifmResultCode != 0)
            {
                return _nifmResultCode;
            }
            if ((int)acceptPolicy > 3)
            {
                return ResultCode.InvalidArgument;
            }
            if (_state == NetworkState.AccessPoint || _state == NetworkState.AccessPointCreated)
            {
                return _accessPoint.SetStationAcceptPolicy(acceptPolicy);
            }
            return ResultCode.InvalidState;
        }

        [CommandHipc(208)]
        public ResultCode AddAcceptFilterEntry(ServiceCtx context)
        {
            if (_nifmResultCode != 0)
            {
                return _nifmResultCode;
            }
            throw new NotImplementedException();
        }

        [CommandHipc(209)]
        public ResultCode ClearAcceptFilter(ServiceCtx context)
        {
            if (_nifmResultCode != 0)
            {
                return _nifmResultCode;
            }
            throw new NotImplementedException();
        }

        [CommandHipc(300)]
        public ResultCode OpenStation(ServiceCtx context)
        {
            if (_nifmResultCode != 0)
            {
                return _nifmResultCode;
            }
            if (_state != NetworkState.Initialized)
            {
                return ResultCode.InvalidState;
            }
            _accessPoint?.Dispose();
            _accessPoint = null;
            SetState(NetworkState.Station);
            _station?.Dispose();
            _station = new Station(this);
            return ResultCode.Success;
        }

        [CommandHipc(301)]
        public ResultCode CloseStation(ServiceCtx context)
        {
            if (_nifmResultCode != 0)
            {
                return _nifmResultCode;
            }
            if (_state == NetworkState.Station || _state == NetworkState.StationConnected)
            {
                DisconnectImpl(DisconnectReason.DisconnectedByUser);
                SetState(NetworkState.Initialized);
                return ResultCode.Success;
            }
            return ResultCode.InvalidState;
        }

        private void CloseStation()
        {
            _station?.Dispose();
            _station = null;
        }

        [CommandHipc(302)]
        public ResultCode Connect(ServiceCtx context)
        {
            return ConnectImpl(context);
        }

        [CommandHipc(303)]
        public ResultCode ConnectPrivate(ServiceCtx context)
        {
            return ConnectImpl(context, isPrivate: true);
        }

        private ResultCode ConnectImpl(ServiceCtx context, bool isPrivate = false)
        {
            SecurityConfig securityConfig = context.RequestData.ReadStruct<SecurityConfig>();
            SecurityParameter securityParameter = (isPrivate ? context.RequestData.ReadStruct<SecurityParameter>() : default(SecurityParameter));
            UserConfig userConfig = context.RequestData.ReadStruct<UserConfig>();
            uint localCommunicationVersion = context.RequestData.ReadUInt32();
            uint optionUnknown = context.RequestData.ReadUInt32();
            NetworkConfig networkConfig = default(NetworkConfig);
            NetworkInfo networkInfo = default(NetworkInfo);
            if (isPrivate)
            {
                context.RequestData.ReadUInt32();
                networkConfig = context.RequestData.ReadStruct<NetworkConfig>();
            }
            else
            {
                ulong bufferPosition = context.Request.PtrBuff[0].Position;
                byte[] networkInfoBytes = new byte[context.Request.PtrBuff[0].Size];
                context.Memory.Read(bufferPosition, networkInfoBytes);
                networkInfo = LdnHelper.FromBytes<NetworkInfo>(networkInfoBytes);
            }
            if (networkInfo.NetworkId.IntentId.LocalCommunicationId == -1)
            {
                ApplicationControlProperty controlProperty = context.Device.Application.ControlData.Value;
                networkInfo.NetworkId.IntentId.LocalCommunicationId = (long)controlProperty.LocalCommunicationId[0];
            }
            if (!CheckLocalCommunicationIdPermission(context, (ulong)networkInfo.NetworkId.IntentId.LocalCommunicationId))
            {
                return ResultCode.InvalidObject;
            }
            if (_nifmResultCode != 0)
            {
                return _nifmResultCode;
            }
            securityConfig.SecurityMode = CheckDevelopmentSecurityMode(securityConfig.SecurityMode);
            ResultCode resultCode = ResultCode.InvalidArgument;
            if ((int)(securityConfig.SecurityMode - 1) <= 2 && optionUnknown <= 1 && localCommunicationVersion >> 15 == 0 && securityConfig.PassphraseSize <= 64)
            {
                resultCode = ResultCode.VersionTooLow;
                if (localCommunicationVersion >= 0)
                {
                    resultCode = ResultCode.VersionTooHigh;
                    if ((long)localCommunicationVersion <= 32767L)
                    {
                        resultCode = ((_state != NetworkState.Station) ? ResultCode.InvalidState : ((!isPrivate) ? _station.Connect(securityConfig, userConfig, localCommunicationVersion, optionUnknown, networkInfo) : _station.ConnectPrivate(securityConfig, securityParameter, userConfig, localCommunicationVersion, optionUnknown, networkConfig)));
                    }
                }
            }
            return resultCode;
        }

        [CommandHipc(304)]
        public ResultCode Disconnect(ServiceCtx context)
        {
            return DisconnectImpl(DisconnectReason.DisconnectedByUser);
        }

        private ResultCode DisconnectImpl(DisconnectReason disconnectReason)
        {
            if (_nifmResultCode != 0)
            {
                return _nifmResultCode;
            }
            if (disconnectReason <= DisconnectReason.DisconnectedBySystem)
            {
                if (_state == NetworkState.StationConnected)
                {
                    SetState(NetworkState.Station);
                    CloseStation();
                    _disconnectReason = disconnectReason;
                    return ResultCode.Success;
                }
                CloseStation();
                return ResultCode.InvalidState;
            }
            return ResultCode.InvalidArgument;
        }

        [CommandHipc(400)]
        public ResultCode InitializeOld(ServiceCtx context)
        {
            return InitializeImpl(context, context.Process.Pid, 90);
        }

        [CommandHipc(401)]
        public ResultCode Finalize(ServiceCtx context)
        {
            if (_nifmResultCode != 0)
            {
                return _nifmResultCode;
            }
            ResultCode num = FinalizeImpl(isCausedBySystem: false);
            if (num == ResultCode.Success)
            {
                SetDisconnectReason(DisconnectReason.None);
            }
            return num;
        }

        private ResultCode FinalizeImpl(bool isCausedBySystem)
        {
            switch (_state)
            {
                case NetworkState.None:
                    return ResultCode.Success;
                case NetworkState.AccessPoint:
                    CloseAccessPoint();
                    break;
                case NetworkState.AccessPointCreated:
                    {
                        DisconnectReason disconnectReason = ((!isCausedBySystem) ? DisconnectReason.DestroyedByUser : DisconnectReason.DestroyedBySystem);
                        DestroyNetworkImpl(disconnectReason);
                        break;
                    }
                case NetworkState.Station:
                    CloseStation();
                    break;
                case NetworkState.StationConnected:
                    {
                        DisconnectReason disconnectReason = ((!isCausedBySystem) ? DisconnectReason.DisconnectedByUser : DisconnectReason.DisconnectedBySystem);
                        DisconnectImpl(disconnectReason);
                        break;
                    }
            }
            SetState(NetworkState.None);
            NetworkClient?.DisconnectAndStop();
            NetworkClient = null;
            return ResultCode.Success;
        }

        [CommandHipc(402)]
        public ResultCode Initialize(ServiceCtx context)
        {
            new IPAddress(context.RequestData.ReadUInt32());
            new IPAddress(context.RequestData.ReadUInt32());
            return InitializeImpl(context, context.Process.Pid, 90);
        }

        public ResultCode InitializeImpl(ServiceCtx context, ulong pid, int nifmRequestId)
        {
            ResultCode resultCode = ResultCode.InvalidArgument;
            if (nifmRequestId <= 255 && _state != NetworkState.Initialized)
            {
                if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
                {
                    resultCode = (_nifmResultCode = ResultCode.DeviceDisabled);
                }
                else
                {
                    switch (context.Device.Configuration.MultiplayerMode)
                    {
                        case MultiplayerMode.RyuLdn:
                            try
                            {
                                if (!IPAddress.TryParse(LanPlayHost, out var ipAddress))
                                {
                                    ipAddress = Dns.GetHostEntry(LanPlayHost).AddressList[0];
                                }
                                NetworkClient = new MasterServerClient(ipAddress.ToString(), LanPlayPort, context.Device.Configuration);
                            }
                            catch (Exception)
                            {
                                Logger.Error?.Print(LogClass.ServiceLdn, "Could not locate RyuLdn server. Defaulting to stubbed wireless.", "InitializeImpl");
                                NetworkClient = new DisabledLdnClient();
                            }
                            break;
                        case MultiplayerMode.Spacemeowx2Ldn:
                            NetworkClient = new Spacemeowx2LdnClient(this, context.Device.Configuration);
                            break;
                        case MultiplayerMode.NxLdn:
                            NetworkClient = new NxLdnClient(this, context.Device.Configuration);
                            break;
                        case MultiplayerMode.Disabled:
                            NetworkClient = new DisabledLdnClient();
                            break;
                    }
                    NetworkClient.SetGameVersion(context.Device.Application.ControlData.Value.DisplayVersion.ItemsRo.ToArray());
                    resultCode = (_nifmResultCode = ResultCode.Success);
                    _currentPid = pid;
                    SetState(NetworkState.Initialized);
                }
            }
            return resultCode;
        }

        public void Dispose()
        {
            if (NetworkClient != null)
            {
                _station?.Dispose();
                _accessPoint?.Dispose();
            }
            NetworkClient?.DisconnectAndStop();
            NetworkClient = null;
        }
    }
}

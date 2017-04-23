﻿using System;

using BizHawk.Emulation.Common;
using BizHawk.Emulation.Cores.Components.CP1610;

namespace BizHawk.Emulation.Cores.Intellivision
{
	[CoreAttributes(
		"IntelliHawk",
		"BrandonE, Alyosha",
		isPorted: false,
		isReleased: true
		)]
	[ServiceNotApplicable(typeof(ISaveRam), typeof(IDriveLight), typeof(IRegionable))]
	public sealed partial class Intellivision : IEmulator, IStatable, IInputPollable, ISettable<Intellivision.IntvSettings, Intellivision.IntvSyncSettings>
	{
		[CoreConstructor("INTV")]
		public Intellivision(CoreComm comm, GameInfo game, byte[] rom, object Settings, object SyncSettings)
		{
			var ser = new BasicServiceProvider(this);
			ServiceProvider = ser;

			CoreComm = comm;

			_rom = rom;
			_gameInfo = game;

			_settings = (IntvSettings)Settings ?? new IntvSettings();
			_syncSettings = (IntvSyncSettings)SyncSettings ?? new IntvSyncSettings();

			ControllerDeck = new IntellivisionControllerDeck(_syncSettings.Port1, _syncSettings.Port2);
			ControllerDefinition.BoolButtons.Add("Power");
			ControllerDefinition.BoolButtons.Add("Reset");

			_cart = new Intellicart();
			if (_cart.Parse(_rom) == -1)
			{
				_cart = new Cartridge();
				_cart.Parse(_rom);
			}

			_cpu = new CP1610
			{
				ReadMemory = ReadMemory,
				WriteMemory = WriteMemory
			};
			_cpu.Reset();

			_stic = new STIC
			{
				ReadMemory = ReadMemory,
				WriteMemory = WriteMemory
			};
			_stic.Reset();

			_psg = new PSG
			{
				ReadMemory = ReadMemory,
				WriteMemory = WriteMemory
			};
			_psg.Reset();

			ser.Register<IVideoProvider>(_stic);
			ser.Register<ISoundProvider>(_psg);

			Connect();

			LoadExecutiveRom(CoreComm.CoreFileProvider.GetFirmware("INTV", "EROM", true, "Executive ROM is required."));
			LoadGraphicsRom(CoreComm.CoreFileProvider.GetFirmware("INTV", "GROM", true, "Graphics ROM is required."));

			_tracer = new TraceBuffer { Header = _cpu.TraceHeader };
			ser.Register<ITraceable>(_tracer);

			SetupMemoryDomains();
		}

		public IntellivisionControllerDeck ControllerDeck { get; private set; }

		private readonly byte[] _rom;
		private readonly GameInfo _gameInfo;
		private readonly ITraceable _tracer;
		private readonly CP1610 _cpu;
		private readonly STIC _stic;
		private readonly PSG _psg;

		private ICart _cart;
		private int _frame;
		private int _sticRow;

		public void Connect()
		{
			_cpu.SetIntRM(_stic.GetSr1());
			_cpu.SetBusRq(_stic.GetSr2());
			_stic.SetSst(_cpu.GetBusAk());
		}

		public void LoadExecutiveRom(byte[] erom)
		{
			if (erom.Length != 8192)
			{
				throw new ApplicationException("EROM file is wrong size - expected 8192 bytes");
			}

			int index = 0;

			// Combine every two bytes into a word.
			while (index + 1 < erom.Length)
			{
				ExecutiveRom[index / 2] = (ushort)((erom[index++] << 8) | erom[index++]);
			}
		}

		public void LoadGraphicsRom(byte[] grom)
		{
			if (grom.Length != 2048)
			{
				throw new ApplicationException("GROM file is wrong size - expected 2048 bytes");
			}

			GraphicsRom = grom;
		}

		private void GetControllerState()
		{
			InputCallbacks.Call();

			ushort port1 = ControllerDeck.ReadPort1(Controller);
			_psg.Register[15] = (ushort)(0xFF - port1);

			ushort port2 = ControllerDeck.ReadPort2(Controller);
			_psg.Register[14] = (ushort)(0xFF - port2);
		}

		private void HardReset()
		{
			_cpu.Reset();
			_stic.Reset();
			_psg.Reset();

			Connect();

			ScratchpadRam = new byte[240];
			SystemRam = new ushort[352];

			_cart = new Intellicart();
			if (_cart.Parse(_rom) == -1)
			{
				_cart = new Cartridge();
				_cart.Parse(_rom);
			}
		}

		private void SoftReset()
		{
			_cpu.Reset();
			_stic.Reset();
			_psg.Reset();

			Connect();
		}
	}
}

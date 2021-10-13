﻿using System.Device.I2c;
using UnitsNet;

namespace Aether.Devices.Drivers
{
    /// <summary>
    /// Air Quality and Gas VOC Sensor SGP4x
    /// </summary>
    public sealed class Sgp4x : System.IDisposable
    {
        /// <summary>
        /// The default I²C address of this device.
        /// </summary>
        public const int DefaultI2cAddress = 0x59;

        private readonly I2cDevice _device;

        /// <summary>
        /// Instantiates a new <see cref="Sgp4x"/>.
        /// </summary>
        /// <param name="device">The I²C device to operate on.</param>
        public Sgp4x(I2cDevice device)
        {
            _device = device;
            Reset();
        }

        /// <inheritdoc/>
        public void Dispose() =>
            _device.Dispose();

        /// <summary>
        /// Resets the device.
        /// </summary>
        public void Reset()
        {
            _device.WriteByte(0x00);
            _device.WriteByte(0x06);
            Thread.Sleep(1);
        }

        /// <summary>
        /// Gets the serial number of the device.
        /// </summary>
        /// <returns>The serial number of the device.</returns>
        public ReadOnlySpan<byte> GetSerialNumber()
        {
            Span<byte> getSerialNumberCommand = stackalloc byte[2] { 0x36, 0x82 };
            Span<byte> serialNumberWithCRC = stackalloc byte[9];

            // Write get serial number command
            _device.Write(getSerialNumberCommand);

            Thread.Sleep(1);

            // Read first two bytes of serial number (+ CRC)
            Sensirion.ReadUInt16BigEndianAndCRC8(serialNumberWithCRC.Slice(0, 3));
            
            // Read second two bytes of serial number (+ CRC)
            Sensirion.ReadUInt16BigEndianAndCRC8(serialNumberWithCRC.Slice(4, 3));
            
            // Read third two bytes of serial number (+ CRC)
            Sensirion.ReadUInt16BigEndianAndCRC8(serialNumberWithCRC.Slice(7, 3));

            // Read serial number into array excluding CRC bytes
            byte[] serialNumber = new byte[6];

            serialNumber[0] = serialNumberWithCRC[0];
            serialNumber[1] = serialNumberWithCRC[1];
            serialNumber[2] = serialNumberWithCRC[3];
            serialNumber[3] = serialNumberWithCRC[4];
            serialNumber[4] = serialNumberWithCRC[6];
            serialNumber[5] = serialNumberWithCRC[7];

            return new ReadOnlySpan<byte>(serialNumber);
        }

        /// <summary>
        /// Runs a self test on the device.
        /// </summary>
        /// <returns>True if all tests passed. False if one or more tests failed.</returns>
        public bool RunSelfTest()
        {
            Span<byte> runSelfTestCommand = stackalloc byte[2] { 0x28, 0x0E };
            Span<byte> testResultWithCRC = stackalloc byte[3];

            // Write run self test command
            _device.Write(runSelfTestCommand);

            Thread.Sleep(320);

            // Read test result data
            _device.Read(testResultWithCRC);

            Sensirion.ReadUInt16BigEndianAndCRC8(testResultWithCRC);

            // Check result status (Most significant byte, ignore least significant non-crc byte)
            // 0xD4 = All tests passed
            // 0x4B = One or more test failed
            return testResultWithCRC[0] == 0xD4;
        }

        /// <summary>
        /// Gets the raw VOC measurements from the device.
        /// </summary>
        /// <param name="relativeHumidityValue">The relative humidity value expressed as a percentage.</param>
        /// <param name="temperatureValue">The temperature value expressed in degrees celsius.</param>
        /// <returns>The raw VOC measurement value from the sensor. If an error occurred, it will be <see langword="null"/>.</returns>
        /// <remarks>If default relative humidity and temperature values are supplied, humidity compensation will be disabled.</remarks>
        public VolumeConcentration? GetVOCRawMeasure(ushort relativeHumidityValue = 50, short temperatureValue = 25)
        {
            if (relativeHumidityValue > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(relativeHumidityValue), relativeHumidityValue, "A relative humidity percentage value must be between 0 and 100");
            }

            if (temperatureValue < -45 || temperatureValue > 130)
            {
                throw new ArgumentOutOfRangeException(nameof(temperatureValue), temperatureValue, "A temperature value must be between -45 and 130");
            }

            Span<byte> writeBuffer = stackalloc byte[8];
            Span<byte> readBuffer = stackalloc byte[3];

            // Write start measure VOC raw command
            writeBuffer[0] = 0x26;
            writeBuffer[1] = 0x0F;

            // Write default humidity value + CRC (0x80, 0x00, [CRC], 0xA2)
            Sensirion.WriteUInt16BigEndianAndCRC8(writeBuffer.Slice(2, 3), (ushort)Math.Round((double)(relativeHumidityValue * 65535.00 / 100.00), MidpointRounding.AwayFromZero));
            
            // Write default temperature value + CRC (0x66, 0x66, [CRC], 0x93)
            Sensirion.WriteUInt16BigEndianAndCRC8(writeBuffer.Slice(5, 3), (ushort)((temperatureValue + 45) * 65535.00 / 175.00));
            
            // Transmit command
            _device.Write(writeBuffer);

            Thread.Sleep(30);

            _device.Read(readBuffer);

            // Read the results and validate CRC
            ushort? readValue = Sensirion.ReadUInt16BigEndianAndCRC8(readBuffer);

            if(readValue is null)
            {
                return null;
            }

            Console.WriteLine("VOC RAW: {0}", readValue.Value);

            Sgp4xAlgorithm.VocAlgorithmParams algoParams = new Sgp4xAlgorithm.VocAlgorithmParams();

            Sgp4xAlgorithm.VocAlgorithm_init(algoParams);

            int vocValue = -1;
            Sgp4xAlgorithm.VocAlgorithm_process(algoParams, readValue.Value, out vocValue);

            Console.WriteLine("VOC INDEX: {0}", vocValue);

            return new VolumeConcentration(vocValue, UnitsNet.Units.VolumeConcentrationUnit.PartPerBillion);
        }

        /// <summary>
        /// Disables the hot plate on the device.
        /// </summary>
        public void DisableHotPlate()
        {
            Span<byte> disableHotPlateCommand = stackalloc byte[2] { 0x36, 0x15 };
            _device.Write(disableHotPlateCommand);
            Thread.Sleep(1);
        }
    }
}

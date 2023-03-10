using System;
using System.Collections.Generic;
using Telemetry.Connection;
using Telemetry.Processing;
using Telemetry.Processing.Calculation;
using Telemetry.Protocol.Datapool;
using Telemetry.Protocol.Transmission;
using Telemetry.Read;
using Telemetry.Utilities;

namespace Telemetry.Protocol
{
    public abstract class TelemetryProtocolProcessor<T> : IGameDataProcessor
    {
        public Action<TelemetryDatapool> ProcessedCallback { get; set; }

        /* submodules */
        protected readonly List<ITelemetryCalculation> calculations = new List<ITelemetryCalculation>();
        protected ITransmitConnection connection;
        private readonly ProtocolPacketConverter packetConverter;
        private readonly ProtocolPacketHeader packetHeader;

        /* data properties */
        private T dataStructure = Activator.CreateInstance<T>();
        private readonly TelemetryDatapool datapool = new TelemetryDatapool(false);

        /* constructor */
        protected TelemetryProtocolProcessor()
        {
            InitValues(datapool);
            packetConverter = new ProtocolPacketConverter(skipUnchangedValues: true);
            packetHeader = new ProtocolPacketHeader(2);
        }

        protected TelemetryProtocolProcessor(ITransmitConnection connection) : this()
        {
            this.connection = connection;
        }

        /* */
        public void AddCalculation(ITelemetryCalculation calculation)
        {
            calculations.Add(calculation);
        }

        public void RemoveCalculation(ITelemetryCalculation calculation)
        {
            calculations.Remove(calculation);
        }

        /* processing interface */
        public void ProcessData(GameData data)
        {
            ConvertRawDataToStructure(data);
            WriteValuesIntoDatapool(dataStructure, datapool);

            // Execute calculations
            calculations.ForEach((calculation) =>
            {
                calculation.Calculate(datapool);
            });

            if (connection != null)
            {
                // convert datapool to raw data
                var valueArray = datapool.ValueArray;
                var byteData = packetConverter.GetBytesFromValues(valueArray);
                packetHeader.ValueCount = (short)valueArray.Length;

                // assemble complete packet data
                var sendData = new byte[byteData.Length + packetHeader.HeaderData.Length];
                Buffer.BlockCopy(packetHeader.HeaderData, 0, sendData, 0, packetHeader.HeaderData.Length);
                Buffer.BlockCopy(byteData, 0, sendData, packetHeader.HeaderData.Length, byteData.Length);

                // transmit packet through connection
                connection.Send(ref sendData);
            }

            ProcessedCallback?.Invoke(datapool);
        }

        private void ConvertRawDataToStructure(GameData data)
        {
            var bytes = data.RawData;
            StructMarshal.MarshalRawDataToStruct(bytes, ref dataStructure);
        }

        /* specific implementation per data structure */
        internal abstract void InitValues(TelemetryDatapool datapool);

        internal abstract void WriteValuesIntoDatapool(T dataStructure ,TelemetryDatapool datapool);
    }
}

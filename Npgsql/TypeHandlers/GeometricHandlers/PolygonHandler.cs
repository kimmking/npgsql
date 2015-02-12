﻿using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Npgsql.BackendMessages;
using NpgsqlTypes;

namespace Npgsql.TypeHandlers.GeometricHandlers
{
    /// <summary>
    /// Type handler for the PostgreSQL geometric polygon type.
    /// </summary>
    /// <remarks>
    /// http://www.postgresql.org/docs/9.4/static/datatype-geometric.html
    /// </remarks>
    // TODO: Should be chunking
    [TypeMapping("polygon", NpgsqlDbType.Polygon, typeof(NpgsqlPolygon))]
    internal class PolygonHandler : TypeHandler<NpgsqlPolygon>,
        IChunkingTypeReader<NpgsqlPolygon>, IChunkingTypeWriter
    {
        #region State

        NpgsqlPolygon _value;
        NpgsqlBuffer _buf;
        int _index;

        #endregion

        #region Read

        public void PrepareRead(NpgsqlBuffer buf, FieldDescription fieldDescription, int len)
        {
            _buf = buf;
            _index = -1;
        }

        public bool Read(out NpgsqlPolygon result)
        {
            result = default(NpgsqlPolygon);

            if (_index == -1)
            {
                if (_buf.ReadBytesLeft < 4) { return false; }
                var numPoints = _buf.ReadInt32();
                _value = new NpgsqlPolygon(numPoints);
                _index = 0;
            }

            for (; _index < _value.Capacity; _index++) {
                if (_buf.ReadBytesLeft < 16) { return false; }
                _value.Add(new NpgsqlPoint(_buf.ReadDouble(), _buf.ReadDouble()));
            }
            result = _value;
            _value = default(NpgsqlPolygon);
            _buf = null;
            return true;
        }

        #endregion

        #region Write

        public int ValidateAndGetLength(object value, ref LengthCache lengthCache)
        {
            if (!(value is NpgsqlPolygon)) {
                throw new InvalidCastException("Expected an NpgsqlPolygon");
            }
            return 4 + ((NpgsqlPolygon)value).Count * 16;
        }

        public void PrepareWrite(object value, NpgsqlBuffer buf, LengthCache lengthCache)
        {
            _buf = buf;
            _value = (NpgsqlPolygon)value;
            _index = -1;
        }

        public bool Write(ref byte[] directBuf)
        {
            if (_index == -1)
            {
                if (_buf.WriteSpaceLeft < 4) { return false; }
                _buf.WriteInt32(_value.Count);
                _index = 0;
            }

            for (; _index < _value.Count; _index++)
            {
                if (_buf.WriteSpaceLeft < 16) { return false; }
                var p = _value[_index];
                _buf.WriteDouble(p.X);
                _buf.WriteDouble(p.Y);
            }
            _buf = null;
            _value = default(NpgsqlPolygon);
            return true;
        }

        #endregion

#if OLD
        public NpgsqlPolygon Read(NpgsqlBuffer buf, FieldDescription fieldDescription, int len)
        {
            var numPoints = buf.ReadInt32();
            var points = new List<NpgsqlPoint>(numPoints);
            for (var i = 0; i < numPoints; i++) {
                if (buf.ReadBytesLeft < sizeof(double) * 2)
                    buf.Ensure(Math.Min(sizeof(double) * 2 * (numPoints - i), buf.Size & -(sizeof(double) * 2)));
                points.Add(new NpgsqlPoint(buf.ReadDouble(), buf.ReadDouble()));
            }
            return new NpgsqlPolygon(points);
        }

        string ISimpleTypeReader<string>.Read(NpgsqlBuffer buf, FieldDescription fieldDescription, int len)
        {
            return Read(buf, fieldDescription, len).ToString();
        }
#endif
    }
}

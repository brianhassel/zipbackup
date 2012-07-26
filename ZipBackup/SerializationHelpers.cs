using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Xml;

namespace BrianHassel.ZipBackup {
    public static class SerializationHelpers {
        public static void SerializeObjectToFile<T>(T obj, FileInfo file, bool useCompression, bool formatted) {
            file.Refresh();
            if (file.Exists)
                file.Delete();

            if (formatted)
                useCompression = false;

            using (var fileStream = file.OpenWrite()) {
                var serializer = new DataContractSerializer(typeof(T));
                if (useCompression) {
                    using (var zip = new DeflateStream(fileStream, CompressionMode.Compress, true)) {
                        serializer.WriteObject(zip, obj);
                    }
                } else {
                    if (formatted) {
                        var settings = new XmlWriterSettings { Indent = true };
                        using (var xmlWriter = XmlWriter.Create(fileStream, settings)) {
                            serializer.WriteObject(xmlWriter, obj);
                        }
                    } else
                        serializer.WriteObject(fileStream, obj);
                }
            }
        }

        public static T DeserializeObjectFromFile<T>(FileInfo file, bool useCompression) {
            file.Refresh();
            if (!file.Exists)
                throw new IOException("Cannot load config. File does not exist: " + file.FullName);

            using (var memory = file.OpenRead()) {
                var formatter = new DataContractSerializer(typeof(T));

                if (useCompression) {
                    using (var zip = new DeflateStream(memory, CompressionMode.Decompress, true)) {
                        return (T)formatter.ReadObject(zip);
                    }
                }

                return (T)formatter.ReadObject(memory);
            }
        }
    }
}

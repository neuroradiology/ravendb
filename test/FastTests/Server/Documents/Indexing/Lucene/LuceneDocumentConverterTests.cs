﻿using Lucene.Net.Documents;
using Raven.Client;
using Raven.Client.Documents.Indexes;
using Raven.Server.Documents.Indexes;
using Raven.Server.Documents.Indexes.Persistence.Lucene.Documents;
using Sparrow.Collections;
using Sparrow.Json;
using Sparrow.Json.Parsing;
using Xunit;
using Document = Raven.Server.Documents.Document;

namespace FastTests.Server.Documents.Indexing.Lucene
{
    public class LuceneDocumentConverterTests : RavenLowLevelTestBase
    {
        private LuceneDocumentConverter _sut;

        private readonly JsonOperationContext _ctx;
        private readonly ConcurrentSet<BlittableJsonReaderObject> _docs = new ConcurrentSet<BlittableJsonReaderObject>();
        private readonly ConcurrentSet<LazyStringValue> _lazyStrings = new ConcurrentSet<LazyStringValue>();

        public LuceneDocumentConverterTests()
        {
            _ctx = JsonOperationContext.ShortTermSingleUse();
        }

        [Fact]
        public void Returns_null_value_if_property_is_null()
        {
            _sut = new LuceneDocumentConverter(new IndexField[]
            {
                new IndexField
                {
                    Name = "Name",
                    Storage = FieldStorage.No
                },
            });

            var doc = create_doc(new DynamicJsonValue
            {
                ["Name"] = null
            }, "users/1");

            bool shouldSkip;
            _sut.SetDocument(doc.Id, doc, _ctx, out shouldSkip);

            Assert.Equal(2, _sut.Document.GetFields().Count);
            Assert.Equal(Constants.Documents.Indexing.Fields.NullValue, _sut.Document.GetField("Name").StringValue(null));
            Assert.Equal("users/1", _sut.Document.GetField(Constants.Documents.Indexing.Fields.DocumentIdFieldName).StringValue(null));
        }

        [Fact]
        public void Returns_empty_string_value_if_property_has_empty_string()
        {
            _sut = new LuceneDocumentConverter(new IndexField[]
            {
                new IndexField
                {
                    Name = "Name",
                    Storage = FieldStorage.No
                },
            });

            var doc = create_doc(new DynamicJsonValue
            {
                ["Name"] = string.Empty
            }, "users/1");

            bool shouldSkip;
            _sut.SetDocument(doc.Id, doc, _ctx, out shouldSkip);

            Assert.Equal(2, _sut.Document.GetFields().Count);
            Assert.Equal(Constants.Documents.Indexing.Fields.EmptyString, _sut.Document.GetField("Name").StringValue(null));
            Assert.Equal("users/1", _sut.Document.GetField(Constants.Documents.Indexing.Fields.DocumentIdFieldName).StringValue(null));
        }

        [Fact]
        public void Conversion_of_string_value()
        {
            _sut = new LuceneDocumentConverter(new IndexField[]
            {
                new IndexField
                {
                    Name = "Name",
                    Storage = FieldStorage.No
                },
            });

            var doc = create_doc(new DynamicJsonValue
            {
                ["Name"] = "Arek"
            }, "users/1");

            bool shouldSkip;
            _sut.SetDocument(doc.Id, doc, _ctx, out shouldSkip);

            Assert.Equal(2, _sut.Document.GetFields().Count);
            Assert.NotNull(_sut.Document.GetField("Name"));
            Assert.Equal("users/1", _sut.Document.GetField(Constants.Documents.Indexing.Fields.DocumentIdFieldName).StringValue(null));
        }

        [Fact]
        public void Reuses_cached_document_instance()
        {
            _sut = new LuceneDocumentConverter(new IndexField[]
            {
                new IndexField
                {
                    Name = "Name",
                    Storage = FieldStorage.No
                },
            });

            var doc1 = create_doc(new DynamicJsonValue
            {
                ["Name"] = "Arek"
            }, "users/1");

            bool shouldSkip;
            _sut.SetDocument(doc1.Id, doc1, _ctx, out shouldSkip);

            Assert.Equal("Arek", _sut.Document.GetField("Name").ReaderValue.ReadToEnd());
            Assert.Equal("users/1", _sut.Document.GetField(Constants.Documents.Indexing.Fields.DocumentIdFieldName).StringValue(null));

            var doc2 = create_doc(new DynamicJsonValue
            {
                ["Name"] = "Pawel"
            }, "users/2");

            _sut.SetDocument(doc2.Id, doc2, _ctx, out shouldSkip);

            Assert.Equal("Pawel", _sut.Document.GetField("Name").ReaderValue.ReadToEnd());
            Assert.Equal("users/2", _sut.Document.GetField(Constants.Documents.Indexing.Fields.DocumentIdFieldName).StringValue(null));
        }

        [Fact]
        public void Conversion_of_numeric_fields()
        {
            _sut = new LuceneDocumentConverter(new IndexField[]
            {
                new IndexField
                {
                    Name = "Weight",
                    Storage = FieldStorage.No,
                },
                new IndexField
                {
                    Name = "Age",
                    Storage = FieldStorage.No,
                },
            });

            var doc = create_doc(new DynamicJsonValue
            {
                ["Weight"] = 70.1,
                ["Age"] = 25,
            }, "users/1");

            bool shouldSkip;
            _sut.SetDocument(doc.Id, doc, _ctx, out shouldSkip);

            Assert.Equal(1 + 2 * 3, _sut.Document.GetFields().Count); // __document_id + 2x (field, field_L_Range, field_D_Range)
            Assert.NotNull(_sut.Document.GetField("Weight"));
            var weightNumeric = _sut.Document.GetFieldable("Weight_D_Range") as NumericField;
            Assert.NotNull(weightNumeric);
            Assert.Equal(70.1, weightNumeric.NumericValue);
            weightNumeric = _sut.Document.GetFieldable("Weight_L_Range") as NumericField;
            Assert.NotNull(weightNumeric);
            Assert.Equal(70L, weightNumeric.NumericValue);
            Assert.NotNull(_sut.Document.GetField("Age"));
            var ageNumeric = _sut.Document.GetFieldable("Age_L_Range") as NumericField;
            Assert.NotNull(ageNumeric);
            Assert.Equal(25L, ageNumeric.NumericValue);
            ageNumeric = _sut.Document.GetFieldable("Age_D_Range") as NumericField;
            Assert.NotNull(ageNumeric);
            Assert.Equal(25.0, ageNumeric.NumericValue);

            Assert.Equal("users/1", _sut.Document.GetField(Constants.Documents.Indexing.Fields.DocumentIdFieldName).StringValue(null));
        }

        [Fact]
        public void Conversion_of_nested_string_value()
        {
            _sut = new LuceneDocumentConverter(new IndexField[]
            {
                new IndexField
                {
                    Name = "Address.City",
                    Storage = FieldStorage.No
                },
            });

            var doc = create_doc(new DynamicJsonValue
            {
                ["Address"] = new DynamicJsonValue
                {
                    ["City"] = "NYC"
                }
            }, "users/1");

            bool shouldSkip;
            _sut.SetDocument(doc.Id, doc, _ctx, out shouldSkip);

            Assert.Equal(2, _sut.Document.GetFields().Count);
            Assert.Equal("NYC", _sut.Document.GetField("Address.City").ReaderValue.ReadToEnd());
            Assert.Equal("users/1", _sut.Document.GetField(Constants.Documents.Indexing.Fields.DocumentIdFieldName).StringValue(null));
        }

        [Fact]
        public void Conversion_of_string_value_nested_inside_collection()
        {
            _sut = new LuceneDocumentConverter(new IndexField[]
            {
                new IndexField
                {
                    Name = "Friends[].Name",
                    Storage = FieldStorage.No
                },
            });

            var doc = create_doc(new DynamicJsonValue
            {
                ["Friends"] = new DynamicJsonArray
                {
                    new DynamicJsonValue
                    {
                        ["Name"] = "Joe"
                    },
                    new DynamicJsonValue
                    {
                        ["Name"] = "John"
                    }
                }
            }, "users/1");

            bool shouldSkip;
            _sut.SetDocument(doc.Id, doc, _ctx, out shouldSkip);

            Assert.Equal(4, _sut.Document.GetFields().Count);
            Assert.Equal(2, _sut.Document.GetFields("Friends[].Name").Length);

            Assert.Equal("Joe", _sut.Document.GetFields("Friends[].Name")[0].ReaderValue.ReadToEnd());
            Assert.Equal("John", _sut.Document.GetFields("Friends[].Name")[1].ReaderValue.ReadToEnd());

            Assert.Equal("true", _sut.Document.GetField("Friends[].Name_IsArray").StringValue(null));

            Assert.Equal("users/1", _sut.Document.GetField(Constants.Documents.Indexing.Fields.DocumentIdFieldName).StringValue(null));
        }

        [Fact]
        public void Conversion_of_string_value_nested_inside_double_nested_collections()
        {
            _sut = new LuceneDocumentConverter(new IndexField[]
            {
                new IndexField
                {
                    Name = "Companies[].Products[].Name",
                    Storage = FieldStorage.No
                },
            });

            var doc = create_doc(new DynamicJsonValue
            {
                ["Companies"] = new DynamicJsonArray
                {
                    new DynamicJsonValue
                        {
                            ["Products"] = new DynamicJsonArray
                            {
                                new DynamicJsonValue
                                    {
                                        ["Name"] = "Headphones CX7"
                                    },
                                    new DynamicJsonValue
                                    {
                                        ["Name"] = "Keyboard AD3"
                                    }
                            }
                        },
                        new DynamicJsonValue
                        {
                            ["Products"] = new DynamicJsonArray
                            {
                                new DynamicJsonValue
                                {
                                    ["Name"] = "Optical Mouse V2"
                                }
                            }
                        },
                }
            }, "companies/1");

            bool shouldSkip;
            _sut.SetDocument(doc.Id, doc, _ctx, out shouldSkip);

            Assert.Equal(5, _sut.Document.GetFields().Count);
            Assert.Equal(3, _sut.Document.GetFields("Companies[].Products[].Name").Length);

            Assert.Equal("Headphones CX7", _sut.Document.GetFields("Companies[].Products[].Name")[0].ReaderValue.ReadToEnd());
            Assert.Equal("Keyboard AD3", _sut.Document.GetFields("Companies[].Products[].Name")[1].ReaderValue.ReadToEnd());
            Assert.Equal("Optical Mouse V2", _sut.Document.GetFields("Companies[].Products[].Name")[2].ReaderValue.ReadToEnd());

            Assert.Equal("true", _sut.Document.GetField("Companies[].Products[].Name_IsArray").StringValue(null));

            Assert.Equal("companies/1", _sut.Document.GetField(Constants.Documents.Indexing.Fields.DocumentIdFieldName).StringValue(null));
        }

        [Fact]
        public void Conversion_of_complex_value()
        {
            _sut = new LuceneDocumentConverter(new IndexField[]
            {
                new IndexField
                {
                    Name = "Address",
                    Storage = FieldStorage.No
                },
                new IndexField
                {
                    Name = "ResidenceAddress",
                    Storage = FieldStorage.No
                },
            });

            var doc = create_doc(new DynamicJsonValue
            {
                ["Address"] = new DynamicJsonValue
                {
                    ["City"] = "New York City"
                },
                ["ResidenceAddress"] = new DynamicJsonValue
                {
                    ["City"] = "San Francisco"
                }
            }, "users/1");

            bool shouldSkip;
            using (_sut.SetDocument(doc.Id, doc, _ctx, out shouldSkip))
            {

                Assert.Equal(5, _sut.Document.GetFields().Count);
                Assert.Equal(@"{""City"":""New York City""}", _sut.Document.GetField("Address").StringValue(null));
                Assert.Equal("true", _sut.Document.GetField("Address" + LuceneDocumentConverterBase.ConvertToJsonSuffix).StringValue(null));
                Assert.Equal(@"{""City"":""San Francisco""}", _sut.Document.GetField("ResidenceAddress").StringValue(null));
                Assert.Equal("true", _sut.Document.GetField("ResidenceAddress" + LuceneDocumentConverterBase.ConvertToJsonSuffix).StringValue(null));
                Assert.Equal("users/1", _sut.Document.GetField(Constants.Documents.Indexing.Fields.DocumentIdFieldName).StringValue(null));

                doc = create_doc(new DynamicJsonValue
                {
                    ["Address"] = new DynamicJsonValue
                    {
                        ["City"] = "NYC"
                    },
                    ["ResidenceAddress"] = new DynamicJsonValue
                    {
                        ["City"] = "Washington"
                    }
                }, "users/2");

                _sut.SetDocument(doc.Id, doc, _ctx, out shouldSkip);

                Assert.Equal(5, _sut.Document.GetFields().Count);
                Assert.Equal(@"{""City"":""NYC""}", _sut.Document.GetField("Address").StringValue(null));
                Assert.Equal("true", _sut.Document.GetField("Address" + LuceneDocumentConverterBase.ConvertToJsonSuffix).StringValue(null));
                Assert.Equal(@"{""City"":""Washington""}", _sut.Document.GetField("ResidenceAddress").StringValue(null));
                Assert.Equal("true", _sut.Document.GetField("ResidenceAddress" + LuceneDocumentConverterBase.ConvertToJsonSuffix).StringValue(null));
                Assert.Equal("users/2", _sut.Document.GetField(Constants.Documents.Indexing.Fields.DocumentIdFieldName).StringValue(null));
            }
        }


        [Fact]
        public void Conversion_of_array_value()
        {
            _sut = new LuceneDocumentConverter(new IndexField[]
            {
                new IndexField
                {
                    Name = "Friends",
                    Storage = FieldStorage.No
                },
            });

            var doc = create_doc(new DynamicJsonValue
            {
                ["Friends"] = new DynamicJsonArray()
                {
                    "Dave", "James"
                }
            }, "users/1");

            bool shouldSkip;
            _sut.SetDocument(doc.Id, doc, _ctx, out shouldSkip);

            Assert.Equal(4, _sut.Document.GetFields().Count);
            Assert.Equal("Dave", _sut.Document.GetFields("Friends")[0].ReaderValue.ReadToEnd());
            Assert.Equal("James", _sut.Document.GetFields("Friends")[1].ReaderValue.ReadToEnd());
            Assert.Equal("true", _sut.Document.GetField("Friends" + LuceneDocumentConverterBase.IsArrayFieldSuffix).StringValue(null));
            Assert.Equal("users/1", _sut.Document.GetField(Constants.Documents.Indexing.Fields.DocumentIdFieldName).StringValue(null));
        }

        [Fact]
        public void Conversion_of_array_having_complex_values()
        {
            _sut = new LuceneDocumentConverter(new IndexField[]
            {
                new IndexField
                {
                    Name = "Addresses",
                    Storage = FieldStorage.No
                },
            });

            var doc = create_doc(new DynamicJsonValue
            {
                ["Addresses"] = new DynamicJsonArray()
                {
                    new DynamicJsonValue
                    {
                        ["City"] = "New York City"
                    },
                    new DynamicJsonValue
                    {
                        ["City"] = "NYC"
                    }
                }
            }, "users/1");

            bool shouldSkip;
            using (_sut.SetDocument(doc.Id, doc, _ctx, out shouldSkip))
            {
                Assert.Equal(5, _sut.Document.GetFields().Count);
                Assert.Equal(@"{""City"":""New York City""}", _sut.Document.GetFields("Addresses")[0].StringValue(null));
                Assert.Equal(@"{""City"":""NYC""}", _sut.Document.GetFields("Addresses")[1].StringValue(null));
                Assert.Equal("true", _sut.Document.GetField("Addresses" + LuceneDocumentConverterBase.ConvertToJsonSuffix).StringValue(null));
                Assert.Equal("true", _sut.Document.GetField("Addresses" + LuceneDocumentConverterBase.IsArrayFieldSuffix).StringValue(null));
                Assert.Equal("users/1", _sut.Document.GetField(Constants.Documents.Indexing.Fields.DocumentIdFieldName).StringValue(null));
            }
        }

        public Document create_doc(DynamicJsonValue document, string id)
        {
            var data = _ctx.ReadObject(document, id);

            _docs.Add(data);

            //_lazyStrings.
            var lazyStringValueRegular = _ctx.GetLazyString(id);
            var lazyStringValueLowerCase = _ctx.GetLazyString(id.ToLowerInvariant());

            return new Document
            {
                Data = data,
                Id = lazyStringValueRegular,
                LowerId = lazyStringValueLowerCase
            };
        }

        public override void Dispose()
        {
            foreach (var docReader in _docs)
            {
                docReader.Dispose();
            }

            _ctx.Dispose();

            base.Dispose();
        }
    }
}

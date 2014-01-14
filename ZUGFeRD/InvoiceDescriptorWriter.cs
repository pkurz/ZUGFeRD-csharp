﻿/*
 * Licensed to the Apache Software Foundation (ASF) under one
 * or more contributor license agreements.  See the NOTICE file
 * distributed with this work for additional information
 * regarding copyright ownership.  The ASF licenses this file
 * to you under the Apache License, Version 2.0 (the
 * "License"); you may not use this file except in compliance
 * with the License.  You may obtain a copy of the License at
 * 
 *   http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing,
 * software distributed under the License is distributed on an
 * "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
 * KIND, either express or implied.  See the License for the
 * specific language governing permissions and limitations
 * under the License.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Globalization;
using System.IO;


namespace s2industries.ZUGFeRD
{
    internal class InvoiceDescriptorWriter
    {
        private XmlTextWriter Writer;
        private InvoiceDescriptor Descriptor;


        public void Save(InvoiceDescriptor descriptor, FileStream stream)
        {
            this.Descriptor = descriptor;
            this.Writer = new XmlTextWriter(stream, Encoding.UTF8);
            Writer.Formatting = Formatting.Indented;
            Writer.WriteStartDocument();

            #region Kopfbereich
            Writer.WriteStartElement("rsm:Invoice");
            Writer.WriteAttributeString("xmlns", "xs", null, "http://www.w3.org/2001/XMLSchema");
            Writer.WriteAttributeString("xmlns", "rsm", null, "urn:un:unece:uncefact:data:standard:CBFBUY:5");
            Writer.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");
            Writer.WriteAttributeString("xsi", "schemaLocation", null, "urn:un:unece:uncefact:data:standard:CBFBUY:5 ../Schema/Invoice.xsd");
            #endregion

            #region SpecifiedExchangedDocumentContext
            Writer.WriteStartElement("rsm:SpecifiedExchangedDocumentContext");
            Writer.WriteElementString("TestIndicator", this.Descriptor.IsTest ? "true" : "false");
            Writer.WriteStartElement("GuidelineSpecifiedDocumentContextParameter");
            Writer.WriteElementString("ID", this.Descriptor.Profile.ToString());
            Writer.WriteEndElement(); // !GuidelineSpecifiedDocumentContextParameter
            Writer.WriteEndElement(); // !rsm:SpecifiedExchangedDocumentContext

            Writer.WriteStartElement("rsm:HeaderExchangedDocument");
            Writer.WriteElementString("ID", this.Descriptor.InvoiceNo);
            Writer.WriteElementString("Name", _translateInvoiceType(this.Descriptor.Type));
            Writer.WriteElementString("TypeCode", String.Format("{0}", _encodeInvoiceType(this.Descriptor.Type)));
            Writer.WriteStartElement("IssueDateTime");
            Writer.WriteAttributeString("format", "102");
            Writer.WriteValue(_formatDate(this.Descriptor.InvoiceDate));
            Writer.WriteEndElement(); // !IssueDateTime()
            _writeOptionalNotes(Writer);
            Writer.WriteEndElement(); // !rsm:HeaderExchangedDocument
            #endregion

            #region SpecifiedSupplyChainTradeTransaction
            Writer.WriteStartElement("rsm:SpecifiedSupplyChainTradeTransaction");
            Writer.WriteStartElement("ApplicableSupplyChainTradeAgreement");
            Writer.WriteElementString("BuyerReference", this.Descriptor.ReferenceOrderNo);

            _writeOptionalParty(Writer, "SellerTradeParty", this.Descriptor.Seller, TaxRegistrations: this.Descriptor.SellerTaxRegistration);
            _writeOptionalParty(Writer, "BuyerTradeParty", this.Descriptor.Buyer, this.Descriptor.BuyerContact, TaxRegistrations: this.Descriptor.BuyerTaxRegistration);

            if ((this.Descriptor.OrderDate != DateTime.MinValue) && (this.Descriptor.OrderNo.Length > 0))
            {
                Writer.WriteStartElement("BuyerOrderReferencedDocument");
                Writer.WriteStartElement("IssueDateTime");
                Writer.WriteAttributeString("format", "102");
                Writer.WriteValue(_formatDate(this.Descriptor.OrderDate));
                Writer.WriteEndElement(); // !IssueDateTime()
                Writer.WriteElementString("ID", this.Descriptor.OrderNo);
                Writer.WriteEndElement(); // !BuyerOrderReferencedDocument
            }

            Writer.WriteEndElement(); // !ApplicableSupplyChainTradeAgreement

            Writer.WriteStartElement("ApplicableSupplyChainTradeDelivery");
            if (this.Descriptor.ActualDeliveryDate != DateTime.MinValue)
            {
                Writer.WriteStartElement("ActualDeliverySupplyChainEvent");
                Writer.WriteStartElement("OccurrenceDateTime");
                Writer.WriteAttributeString("format", "102");
                Writer.WriteValue(_formatDate(this.Descriptor.ActualDeliveryDate));
                Writer.WriteEndElement(); // !IssueDateTime()
                Writer.WriteEndElement(); // !DeliveryNoteReferencedDocument
            }

            if ((this.Descriptor._DeliveryNoteDate != DateTime.MinValue) && (this.Descriptor._DeliveryNoteNo.Length > 0))
            {
                Writer.WriteStartElement("DeliveryNoteReferencedDocument");
                Writer.WriteElementString("ID", this.Descriptor._DeliveryNoteNo);
                Writer.WriteStartElement("IssueDateTime");
                Writer.WriteAttributeString("format", "102");
                Writer.WriteValue(_formatDate(this.Descriptor._DeliveryNoteDate));
                Writer.WriteEndElement(); // !IssueDateTime()
                Writer.WriteEndElement(); // !DeliveryNoteReferencedDocument
            }
            Writer.WriteEndElement(); // !ApplicableSupplyChainTradeDelivery

            Writer.WriteStartElement("ApplicableSupplyChainTradeSettlement");
            _writeOptionalElementString(Writer, "PaymentReference", this.Descriptor._InvoiceNoAsReference);
            Writer.WriteElementString("InvoiceCurrencyCode", this.Descriptor.Currency.ToString());
            _writeOptionalTaxes(Writer);

            if ((this.Descriptor._TradeAllowanceCharges != null) && (this.Descriptor._TradeAllowanceCharges.Count > 0))
            {
                foreach (TradeAllowanceCharge tradeAllowanceCharge in this.Descriptor._TradeAllowanceCharges)
                {
                    Writer.WriteStartElement("SpecifiedTradeAllowanceCharge");
                    Writer.WriteElementString("ChargeIndicator", tradeAllowanceCharge.ChargeIndicator ? "true" : "false");

                    Writer.WriteStartElement("BasisAmount");
                    Writer.WriteAttributeString("currencyID", tradeAllowanceCharge.Currency.ToString("g"));
                    Writer.WriteValue(_formatCurrency(tradeAllowanceCharge.BasisAmount));
                    Writer.WriteEndElement();
                    Writer.WriteElementString("ActualAmount", _formatCurrency(tradeAllowanceCharge.Amount));

                    _writeOptionalElementString(Writer, "Reason", tradeAllowanceCharge.Reason);

                    if (tradeAllowanceCharge.Tax != null)
                    {
                        Writer.WriteStartElement("CategoryTradeTax");
                        Writer.WriteElementString("TypeCode", _translateTaxType(tradeAllowanceCharge.Tax.TypeCode));
                        Writer.WriteElementString("CategoryCode", _translateTaxCategoryCode(tradeAllowanceCharge.Tax.CategoryCode));
                        Writer.WriteElementString("ApplicablePercent", tradeAllowanceCharge.Tax.Percent.ToString("#"));
                        Writer.WriteEndElement();
                    }
                    Writer.WriteEndElement();
                }
            }

            if ((this.Descriptor._ServiceCharges != null) && (this.Descriptor._ServiceCharges.Count > 0))
            {
                foreach (ServiceCharge serviceCharge in this.Descriptor._ServiceCharges)
                {
                    Writer.WriteStartElement("SpecifiedLogisticsServiceCharge");
                    if (serviceCharge.Description.Length > 0)
                    {
                        Writer.WriteElementString("Description", serviceCharge.Description);
                    }
                    Writer.WriteElementString("AppliedAmount", _formatCurrency(serviceCharge.Amount));
                    if (serviceCharge.Tax != null)
                    {
                        Writer.WriteStartElement("AppliedTradeTax");
                        Writer.WriteElementString("TypeCode", _translateTaxType(serviceCharge.Tax.TypeCode));
                        Writer.WriteElementString("CategoryCode", _translateTaxCategoryCode(serviceCharge.Tax.CategoryCode));
                        Writer.WriteElementString("ApplicablePercent", serviceCharge.Tax.Percent.ToString("#"));
                        Writer.WriteEndElement();
                    }
                    Writer.WriteEndElement();
                }
            }

            if (this.Descriptor._PaymentTerms != null)
            {
                Writer.WriteStartElement("SpecifiedTradePaymentTerms");
                _writeOptionalElementString(Writer, "Description", this.Descriptor._PaymentTerms.Description);
                _writeElementWithAttribute(Writer, "DueDateDateTime", "format", "102", _formatDate(this.Descriptor._PaymentTerms.DueDate));
                Writer.WriteEndElement();
            }

            Writer.WriteStartElement("SpecifiedTradeSettlementMonetarySummation");
            _writeOptionalAmount(Writer, "LineTotalAmount", this.Descriptor.LineTotalAmount);
            _writeOptionalAmount(Writer, "ChargeTotalAmount", this.Descriptor.ChargeTotalAmount);
            _writeOptionalAmount(Writer, "AllowanceTotalAmount", this.Descriptor.AllowanceTotalAmount);
            _writeOptionalAmount(Writer, "TaxBasisTotalAmount", this.Descriptor.TaxBasisAmount);
            _writeOptionalAmount(Writer, "TaxTotalAmount", this.Descriptor.TaxTotalAmount);
            _writeOptionalAmount(Writer, "GrandTotalAmount", this.Descriptor.GrandTotalAmount);
            _writeOptionalAmount(Writer, "TotalPrepaidAmount", this.Descriptor.TotalPrepaidAmount);
            _writeOptionalAmount(Writer, "DuePayableAmount", this.Descriptor.DuePayableAmount);
            Writer.WriteEndElement(); // !SpecifiedTradeSettlementMonetarySummation

            Writer.WriteEndElement(); // !ApplicableSupplyChainTradeSettlement


            for (int i = 0; i < this.Descriptor.TradeLineItems.Count; i++)
            {
                TradeLineItem tradeLineItem = this.Descriptor.TradeLineItems[i];
                Writer.WriteStartElement("IncludedSupplyChainTradeLineItem");
                Writer.WriteStartElement("AssociatedDocumentLineDocument");
                Writer.WriteElementString("LineID", (i+1).ToString());
                Writer.WriteEndElement(); // AssociatedDocumentLineDocument

                Writer.WriteStartElement("SpecifiedSupplyChainTradeAgreement");
                
                Writer.WriteStartElement("GrossPriceProductTradePrice");
                _writeOptionalAmount(Writer, "ChargeAmount", tradeLineItem.GrossUnitPrice);
                _writeElementWithAttribute(Writer, "BasisQuantity", "unitCode", _translateQuantityCode(tradeLineItem.UnitCode), tradeLineItem.UnitQuantity.ToString());
                Writer.WriteEndElement(); // GrossPriceProductTradePrice

                Writer.WriteStartElement("NetPriceProductTradePrice");
                _writeOptionalAmount(Writer, "ChargeAmount", tradeLineItem.NetUnitPrice);
                _writeElementWithAttribute(Writer, "BasisQuantity", "unitCode", _translateQuantityCode(tradeLineItem.UnitCode), tradeLineItem.UnitQuantity.ToString());
                Writer.WriteEndElement(); // NetPriceProductTradePrice

                Writer.WriteEndElement(); // !SpecifiedSupplyChainTradeAgreement

                Writer.WriteStartElement("SpecifiedSupplyChainTradeDelivery");
                _writeElementWithAttribute(Writer, "BilledQuantity", "unitCode", _translateQuantityCode(tradeLineItem.UnitCode), tradeLineItem.BilledQuantity.ToString());
                Writer.WriteEndElement(); // !SpecifiedSupplyChainTradeDelivery

                Writer.WriteStartElement("SpecifiedSupplyChainTradeSettlement");
                Writer.WriteStartElement("ApplicableTradeTax");
                Writer.WriteElementString("TypeCode", tradeLineItem.TaxType.ToString());
                Writer.WriteElementString("CategoryCode", tradeLineItem.TaxCategoryCode.ToString());
                Writer.WriteElementString("ApplicablePercent", tradeLineItem.TaxPercent.ToString());
                Writer.WriteEndElement(); // !ApplicableTradeTax
                Writer.WriteStartElement("SpecifiedTradeSettlementMonetarySummation");
                decimal _total = tradeLineItem.NetUnitPrice * tradeLineItem.BilledQuantity;
                _writeElementWithAttribute(Writer, "LineTotalAmount", "currencyID", this.Descriptor.Currency.ToString(), _formatCurrency(_total));
                Writer.WriteEndElement(); // SpecifiedTradeSettlementMonetarySummation
                Writer.WriteEndElement(); // !SpecifiedSupplyChainTradeSettlement

                Writer.WriteStartElement("SpecifiedTradeProduct");
                _writeElementWithAttribute(Writer, "GlobalID", "schemeID", tradeLineItem.GlobalID.SchemeID, tradeLineItem.GlobalID.ID);
                _writeOptionalElementString(Writer, "SellerAssignedID", tradeLineItem.SellerAssignedID);
                _writeOptionalElementString(Writer, "BuyerAssignedID", tradeLineItem.BuyerAssignedID);
                _writeOptionalElementString(Writer, "Description", tradeLineItem.Description);
                _writeOptionalElementString(Writer, "Name", tradeLineItem.Name);

                Writer.WriteEndElement(); // !SpecifiedTradeProduct

                Writer.WriteEndElement(); // !IncludedSupplyChainTradeLineItem
            } // !foreach(tradeLineItem)

            Writer.WriteEndElement(); // !SpecifiedSupplyChainTradeTransaction
            #endregion

            Writer.WriteEndElement(); // !Invoice
            Writer.WriteEndDocument();

            Writer.Close();
        } // !Save()


        public void Save(InvoiceDescriptor descriptor, string filename)
        {
            Save(descriptor, new FileStream(filename, FileMode.Create, FileAccess.Write));
        } // !Save()


        private void _writeOptionalAmount(XmlTextWriter writer, string tagName, decimal value)
        {
            if (value != decimal.MinValue)
            {
                writer.WriteStartElement(tagName);
                writer.WriteAttributeString("currencyID", this.Descriptor.Currency.ToString());
                writer.WriteValue(_formatCurrency(value));
                writer.WriteEndElement(); // !tagName
            }
        } // !_writeOptionalAmount()


        private void _writeElementWithAttribute(XmlTextWriter writer, string tagName, string attributeName, string attributeValue, string nodeValue)
        {
            writer.WriteStartElement(tagName);
            writer.WriteAttributeString(attributeName, attributeValue);
            writer.WriteValue(nodeValue);
            writer.WriteEndElement(); // !tagName
        } // !_writeElementWithAttribute()


        private void _writeOptionalTaxes(XmlTextWriter writer)
        {
            foreach (Tax tax in this.Descriptor._Taxes)
            {
                writer.WriteStartElement("ApplicableTradeTax");

                writer.WriteStartElement("CalculatedAmount");
                writer.WriteAttributeString("currencyID", this.Descriptor.Currency.ToString());
                writer.WriteValue(_formatCurrency(tax.TaxAmount));
                writer.WriteEndElement(); // !CalculatedAmount

                writer.WriteElementString("TypeCode", _translateTaxType(tax.TypeCode));

                writer.WriteStartElement("BasisAmount");
                writer.WriteAttributeString("currencyID", this.Descriptor.Currency.ToString());
                writer.WriteValue(_formatCurrency(tax.BasisAmount));
                writer.WriteEndElement(); // !BasisAmount

                writer.WriteElementString("CategoryCode", _translateTaxCategoryCode(tax.CategoryCode));
                writer.WriteElementString("ApplicablePercent", tax.Percent.ToString("#"));
                writer.WriteEndElement(); // !ApplicableTradeTax
            }
        } // !_writeOptionalTaxes()


        private void _writeOptionalNotes(XmlTextWriter writer)
        {
            if (this.Descriptor.Notes.Count > 0)
            {
                foreach (Tuple<string, SubjectCodes> t in this.Descriptor.Notes)
                {
                    writer.WriteStartElement("IncludedNote");
                    writer.WriteElementString("Content", t.Item1);
                    if (t.Item2 != SubjectCodes.Unknown)
                    {
                        writer.WriteElementString("SubjectCode", _translateSubjectCode(t.Item2));
                    }
                    writer.WriteEndElement();
                }
            }
        } // !_writeOptionalNotes()


        private void _writeOptionalParty(XmlTextWriter writer, string PartyTag, Party Party, Contact Contact = null, List<TaxRegistration> TaxRegistrations = null)
        {
            if (Party != null)
            {
                writer.WriteStartElement(PartyTag);

                if ((Party.GlobalID != null) && (Party.GlobalID.ID.Length > 0) && (Party.GlobalID.SchemeID.Length > 0))
                {
                    writer.WriteStartElement("GlobalID");
                    writer.WriteAttributeString("schemeID", Party.GlobalID.SchemeID);
                    writer.WriteValue(Party.GlobalID.ID);
                    writer.WriteEndElement();
                }

                writer.WriteElementString("Name", Party.Name);

                if (Contact != null)
                {
                    _writeOptionalContact(writer, "DefinedTradeContact", Contact);
                }

                writer.WriteStartElement("PostalTradeAddress");
                writer.WriteElementString("PostcodeCode", Party.Postcode);
                writer.WriteElementString("LineOne", _formatStreet(Party.Street, Party.StreetNo));
                writer.WriteElementString("CityName", Party.City);
                writer.WriteElementString("CountryID", Party.Country);
                writer.WriteEndElement(); // !PostalTradeAddress

                if (TaxRegistrations != null)
                {
                    foreach (TaxRegistration _reg in TaxRegistrations)
                    {
                        writer.WriteStartElement("SpecifiedTaxRegistration");
                        writer.WriteStartElement("ID");
                        writer.WriteAttributeString("schemeID", _translateTaxRegistrationSchemeID(_reg.SchemeID));
                        writer.WriteValue(_reg.No);
                        writer.WriteEndElement();
                        writer.WriteEndElement();
                    }
                }
                writer.WriteEndElement(); // !*TradeParty
            }
        } // !_writeOptionalParty()


        private void _writeOptionalContact(XmlTextWriter writer, string contactTag, Contact contact)
        {
            if (contact != null)
            {
                writer.WriteStartElement(contactTag);

                if (contact.Name.Length > 0)
                {
                    writer.WriteElementString("Name", contact.Name);
                }

                ///
                /// TODO: restliche Kontaktattribute einpflegen
                ///

                writer.WriteEndElement();
            }
        } // !_writeOptionalContact()


        private void _writeOptionalElementString(XmlTextWriter writer, string tagName, string value)
        {
            if (value.Length > 0)
            {
                writer.WriteElementString(tagName, value);
            }
        } // !_writeOptionalElementString()


        private string _formatCurrency(decimal value)
        {
            return value.ToString("0.00").Replace(",", ".");
        } // !_formatCurrency()


        private string _formatStreet(string street, string streetNo)
        {
            string retval = street;
            if (streetNo.Length > 0)
            {
                retval += " " + streetNo;
            }
            return retval;
        } // !_formatStreet()


        private string _translateInvoiceType(InvoiceType type)
        {
            switch (type)
            {
                case InvoiceType.Invoice: return "RECHNUNG";
                case InvoiceType.Correction: return "KORREKTURRECHNUNG";
                case InvoiceType.CreditNote: return "GUTSCHRIFT";
                case InvoiceType.DebitNote: return "";
                case InvoiceType.SelfBilledInvoice: return "";
                default: return "";
            }
        } // !_translateInvoiceType()


        private int _encodeInvoiceType(InvoiceType type)
        {
            if ((int)type > 1000)
            {
                type -= 1000;
            }

            return (int)type;
        } // !_translateInvoiceType()


        private string _translateTaxRegistrationSchemeID(TaxRegistrationSchemeID schemeID)
        {
            return schemeID.ToString("g");
        } // !_translateTaxRegistrationSchemeID()


        private string _translateSubjectCode(SubjectCodes code)
        {
            return code.ToString("g");
        } // !_translateSubjectCode()


        private string _translateTaxType(TaxTypes type)
        {
            return type.ToString("g");
        } // !_translateTaxType()


        private string _translateTaxCategoryCode(TaxCategoryCodes code)
        {
            return code.ToString("g");
        } // !_translateTaxCategoryCode()


        private string _translateQuantityCode(QuantityCodes code)
        {
            return code.ToString("g");
        } // !_translateQuantityCode()


        private string _formatDate(DateTime date)
        {
            return date.ToString("yyyyMMdd");
        } // !_formatDate()
    }
}

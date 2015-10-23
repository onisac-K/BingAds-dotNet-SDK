﻿using System;
using System.IO;
using System.Linq;
using System.Net;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.BingAds.Reporting;
using Microsoft.BingAds;


namespace BingAdsExamples.V9
{
    /// <summary>
    /// This example demonstrates how to request and retrieve a keyword performance report.
    /// </summary>
    public class PerformanceReports : ExampleBase
    {
        public static ServiceClient<IReportingService> Service;

        // Specify the file to download the report to. The file is
        // compressed so use the .zip file extension.

        private const string DownloadPath = @"c:\reports\keywordperf.zip";


        public override string Description
        {
            get { return "Performance Reports | Reporting V9"; }
        }

        public async override Task RunAsync(AuthorizationData authorizationData)
        {
            try
            {
                Service = new ServiceClient<IReportingService>(authorizationData);

                // Build a keyword performance report request, including Format, ReportName, Aggregation,
                // Scope, Time, Filter, and Columns.

                var report = GetCampaignPerformanceReportRequest(authorizationData.AccountId);

                // SubmitGenerateReport helper method calls the corresponding Bing Ads service operation 
                // to request the report identifier. The identifier is used to check report generation status
                // before downloading the report. 

                var reportRequestId = await SubmitGenerateReportAsync(report);

                OutputStatusMessage("Report Request ID: " + reportRequestId);
                
                var waitTime = new TimeSpan(0, 0, 30);
                ReportRequestStatus reportRequestStatus = null;

                // This example polls every 30 seconds up to 5 minutes.
                // In production you may poll the status every 1 to 2 minutes for up to one hour.
                // If the call succeeds, stop polling. If the call or 
                // download fails, the call throws a fault.

                for (int i = 0; i < 10; i++)
                {
                    OutputStatusMessage(String.Format("Will check if the report is ready in {0} seconds: ", waitTime.Seconds));
                    Thread.Sleep(waitTime);

                    // PollGenerateReport helper method calls the corresponding Bing Ads service operation 
                    // to get the report request status.
                    reportRequestStatus = await PollGenerateReportAsync(reportRequestId);

                    if (reportRequestStatus.Status == ReportRequestStatusType.Success ||
                        reportRequestStatus.Status == ReportRequestStatusType.Error)
                    {
                        break;
                    }

                    OutputStatusMessage("The report is not yet ready for download.");
                }

                if (reportRequestStatus != null)
                {
                    if (reportRequestStatus.Status == ReportRequestStatusType.Success)
                    {
                        var reportDownloadUrl = reportRequestStatus.ReportDownloadUrl;
                        OutputStatusMessage(String.Format("Downloading from {0}.", reportDownloadUrl));
                        OutputStatusMessage("\n");
                        DownloadFile(reportDownloadUrl, DownloadPath);
                        OutputStatusMessage(String.Format("The report was written to {0}.", DownloadPath));
                    }
                    else if (reportRequestStatus.Status == ReportRequestStatusType.Error)
                    {
                        OutputStatusMessage("The request failed. Try requesting the report " +
                            "later.\nIf the request continues to fail, contact support.");
                    }
                    else  // Pending
                    {
                        OutputStatusMessage(String.Format("The request is taking longer than expected.\n " +
                            "Save the report ID ({0}) and try again later.", reportRequestId));
                    }
                }
            }
            // Catch authentication exceptions
            catch (OAuthTokenRequestException ex)
            {
                OutputStatusMessage(string.Format("Couldn't get OAuth tokens. Error: {0}. Description: {1}", ex.Details.Error, ex.Details.Description));
            }
            // Catch Reporting service exceptions
            catch (FaultException<Microsoft.BingAds.Reporting.AdApiFaultDetail> ex)
            {
                OutputStatusMessage(string.Join("; ", ex.Detail.Errors.Select(error => string.Format("{0}: {1}", error.Code, error.Message))));
            }
            catch (FaultException<Microsoft.BingAds.Reporting.ApiFaultDetail> ex)
            {
                OutputStatusMessage(string.Join("; ", ex.Detail.OperationErrors.Select(error => string.Format("{0}: {1}", error.Code, error.Message))));
                OutputStatusMessage(string.Join("; ", ex.Detail.BatchErrors.Select(error => string.Format("{0}: {1}", error.Code, error.Message))));
            }
            catch (WebException ex)
            {
                OutputStatusMessage(ex.Message);

                if (ex.Response != null)
                    OutputStatusMessage("HTTP status code: " + ((HttpWebResponse)ex.Response).StatusCode);
            }
            catch (IOException ex)
            {
                OutputStatusMessage(ex.Message);
            }
            catch (Exception ex)
            {
                OutputStatusMessage(ex.Message);
            }
        }

        // Request the report and returns the ReportRequestId that can be used to check report
        // status and then used to download the report.

        private async Task<string> SubmitGenerateReportAsync(ReportRequest report)
        {
            var request = new SubmitGenerateReportRequest
            {
                ReportRequest = report
            };

            return (await Service.CallAsync((s, r) => s.SubmitGenerateReportAsync(r),request)).ReportRequestId;
        }

        // Checks the status of a report request. Returns a data object that contains both
        // report status and download URL. 

        private async Task<ReportRequestStatus> PollGenerateReportAsync(string reportId)
        {
            var request = new PollGenerateReportRequest
            {
                ReportRequestId = reportId
            };

            return (await Service.CallAsync((s, r) => s.PollGenerateReportAsync(r), request)).ReportRequestStatus;
        }

        // Using the URL that the PollGenerateReport operation returned,
        // send an HTTP request to get the report and write it to the specified
        // ZIP file.

        static void DownloadFile(string reportDownloadUrl, string downloadPath)
        {
            var request = (HttpWebRequest)WebRequest.Create(reportDownloadUrl);
            var response = (HttpWebResponse)request.GetResponse();
            var fileInfo = new FileInfo(downloadPath);
            Stream responseStream = null;
            BinaryWriter binaryWriter = null;
            BinaryReader binaryReader = null;

            // If the folders in the specified path do not exist, create them.

            if (fileInfo.Directory != null && !fileInfo.Directory.Exists)
            {
                fileInfo.Directory.Create();
            }

            // Create the ZIP file.

            var fileStream = new FileStream(fileInfo.FullName, FileMode.Create);

            try
            {
                responseStream = response.GetResponseStream();
                binaryWriter = new BinaryWriter(fileStream);
                if (responseStream != null) binaryReader = new BinaryReader(responseStream);

                const int bufferSize = 100 * 1024;

                while (true)
                {
                    // Read report data from download URL.

                    if (binaryReader != null)
                    {
                        byte[] buffer = binaryReader.ReadBytes(bufferSize);

                        // Write report data to file.

                        binaryWriter.Write(buffer);

                        // If the end of the report is reached, break out of the loop.

                        if (buffer.Length != bufferSize)
                        {
                            break;
                        }
                    }
                }
            }
            finally
            {
                fileStream.Close();
                if (responseStream != null) responseStream.Close();
                if (binaryReader != null) binaryReader.Close();
                if (binaryWriter != null) binaryWriter.Close();
            }            
        }

        private KeywordPerformanceReportRequest GetKeywordPerformanceReportRequest(long accountId)
        {
            var report = new KeywordPerformanceReportRequest
            {
                Format = ReportFormat.Tsv,
                Language = ReportLanguage.English,
                ReportName = "My Keyword Performance Report",
                ReturnOnlyCompleteData = false,
                Aggregation = ReportAggregation.Daily,

                Scope = new AccountThroughAdGroupReportScope
                {
                    AccountIds = new[] { accountId },
                    AdGroups = null,
                    Campaigns = null
                },

                Time = new ReportTime
                {
                    // You may either use a custom date range or predefined time.

                    //CustomDateRangeStart = new Date
                    //    {
                    //        Month = DateTime.Now.Month,
                    //        Day = DateTime.Now.Day,
                    //        Year = DateTime.Now.Year - 1
                    //    },
                    //CustomDateRangeEnd = new Date
                    //    {
                    //    Month = DateTime.Now.Month,
                    //    Day = DateTime.Now.Day,
                    //    Year = DateTime.Now.Year
                    //    },

                    CustomDateRangeStart = new Date
                    {
                        Month = 12,
                        Day = 4,
                        Year = DateTime.Now.Year - 5
                    },
                    CustomDateRangeEnd = new Date
                    {
                        Month = 2,
                        Day = 5,
                        Year = DateTime.Now.Year
                    },

                    //PredefinedTime = ReportTimePeriod.Yesterday
                },

                // If you specify a filter, results may differ from data you see in the Bing Ads web application
                //Filter = new KeywordPerformanceReportFilter
                //{
                //    DeviceType = DeviceTypeReportFilter.Computer |
                //                 DeviceTypeReportFilter.SmartPhone
                //},

                // Specify the attribute and data report columns. 
                Columns = new[]
                            {
                                KeywordPerformanceReportColumn.TimePeriod,
                                KeywordPerformanceReportColumn.AccountId,
                                KeywordPerformanceReportColumn.CampaignId,
                                KeywordPerformanceReportColumn.Keyword,
                                KeywordPerformanceReportColumn.KeywordId,
                                KeywordPerformanceReportColumn.DeviceType,
                                KeywordPerformanceReportColumn.BidMatchType,
                                KeywordPerformanceReportColumn.Clicks,
                                KeywordPerformanceReportColumn.Impressions,
                                KeywordPerformanceReportColumn.Ctr,
                                KeywordPerformanceReportColumn.AverageCpc,
                                KeywordPerformanceReportColumn.Spend,
                                KeywordPerformanceReportColumn.QualityScore,
                                KeywordPerformanceReportColumn.ExtendedCost,
                                KeywordPerformanceReportColumn.LandingPageRelevance,
                                KeywordPerformanceReportColumn.LandingPageUserExperience,
                                KeywordPerformanceReportColumn.Revenue,
                                KeywordPerformanceReportColumn.Assists,
                                KeywordPerformanceReportColumn.KeywordRelevance,
                                KeywordPerformanceReportColumn.DeliveredMatchType,
                                KeywordPerformanceReportColumn.AveragePosition,
                                KeywordPerformanceReportColumn.Conversions,
                                KeywordPerformanceReportColumn.AdDistribution,
                                KeywordPerformanceReportColumn.Network,
                                KeywordPerformanceReportColumn.AdId,
                                KeywordPerformanceReportColumn.AdType,
                                KeywordPerformanceReportColumn.AdGroupId
                            },

                // You may optionally sort by any KeywordPerformanceReportColumn, and optionally
                // specify the maximum number of rows to return in the sorted report. 
                Sort = new[]
                            {
                                new KeywordPerformanceReportSort
                                    {
                                        SortColumn = KeywordPerformanceReportColumn.Clicks,
                                        SortOrder = SortOrder.Ascending
                                    }
                            },

                MaxRows = 10,
            };

            return report;
        }

        private ReportRequest GetCampaignPerformanceReportRequest(long accountId)
        {
            var report = new CampaignPerformanceReportRequest
            {
                Format = ReportFormat.Tsv,
                Language = ReportLanguage.English,
                ReportName = "My Campaign Performance Report",
                ReturnOnlyCompleteData = false,
                Aggregation = ReportAggregation.Daily,

                Scope = new AccountThroughCampaignReportScope
                {
                    AccountIds = new[] { accountId },
                    Campaigns = null
                },

                // Alternatively you can request data for a subset of campaigns.
                //Scope = new AccountThroughCampaignReportScope
                //{
                //    AccountIds = null,
                //    Campaigns = new [] {
                //        new CampaignReportScope
                //        {
                //            AccountId = accountId,
                //            CampaignId = <YourCampaignIdGoesHere>
                //        }
                //    }
                //},

                Time = new ReportTime
                {
                    // You may either use a custom date range or predefined time.

                    //CustomDateRangeStart = new Date
                    //    {
                    //        Month = DateTime.Now.Month,
                    //        Day = DateTime.Now.Day,
                    //        Year = DateTime.Now.Year - 1
                    //    },
                    //CustomDateRangeEnd = new Date
                    //    {
                    //    Month = DateTime.Now.Month,
                    //    Day = DateTime.Now.Day,
                    //    Year = DateTime.Now.Year
                    //    },

                    CustomDateRangeStart = new Date
                    {
                        Month = 12,
                        Day = 4,
                        Year = DateTime.Now.Year - 5
                    },
                    CustomDateRangeEnd = new Date
                    {
                        Month = 2,
                        Day = 5,
                        Year = DateTime.Now.Year
                    },

                    //PredefinedTime = ReportTimePeriod.Yesterday
                },

                // If you specify a filter, results may differ from data you see in the Bing Ads web application
                //Filter = new CampaignPerformanceReportFilter
                //{
                //    DeviceType = DeviceTypeReportFilter.Computer |
                //                 DeviceTypeReportFilter.SmartPhone
                //},

                // Specify the attribute and data report columns. 
                Columns = new[]
                {
                    CampaignPerformanceReportColumn.TimePeriod,
                    CampaignPerformanceReportColumn.AccountId,
                    CampaignPerformanceReportColumn.CampaignId,
                    CampaignPerformanceReportColumn.DeviceType,
                    CampaignPerformanceReportColumn.BidMatchType,
                    CampaignPerformanceReportColumn.QualityScore,
                    CampaignPerformanceReportColumn.ExtendedCost,
                    CampaignPerformanceReportColumn.LandingPageRelevance,
                    CampaignPerformanceReportColumn.LandingPageUserExperience,
                    CampaignPerformanceReportColumn.Revenue,
                    CampaignPerformanceReportColumn.Assists,
                    CampaignPerformanceReportColumn.KeywordRelevance,
                    CampaignPerformanceReportColumn.DeliveredMatchType,
                    CampaignPerformanceReportColumn.AveragePosition,
                    CampaignPerformanceReportColumn.Conversions,
                    CampaignPerformanceReportColumn.AdDistribution,
                    CampaignPerformanceReportColumn.Network,
                    CampaignPerformanceReportColumn.Clicks,
                    CampaignPerformanceReportColumn.Impressions,
                    CampaignPerformanceReportColumn.Ctr,
                    CampaignPerformanceReportColumn.AverageCpc,
                    CampaignPerformanceReportColumn.Spend,
                },

            };

            return report;
        }

        private ReportRequest GetProductDimensionPerformanceReport(long accountId)
        {
            return new ProductDimensionPerformanceReportRequest
            {
                Format = ReportFormat.Tsv,
                ReportName = "My Product Dimension Performance Report",
                ReturnOnlyCompleteData = false,
                Aggregation = ReportAggregation.Daily,

                Scope = new AccountThroughAdGroupReportScope
                {
                    AccountIds = new[] { accountId },
                    AdGroups = null,
                    Campaigns = null
                },

                Time = new ReportTime
                {
                    // You may either use a custom date range or predefined time.

                    //CustomDateRangeStart = new Date
                    //    {
                    //        Month = DateTime.Now.Month,
                    //        Day = DateTime.Now.Day,
                    //        Year = DateTime.Now.Year - 1
                    //    },
                    //CustomDateRangeEnd = new Date
                    //    {
                    //    Month = DateTime.Now.Month,
                    //    Day = DateTime.Now.Day,
                    //    Year = DateTime.Now.Year
                    //    },

                    PredefinedTime = ReportTimePeriod.Yesterday
                },

                // If you specify a filter, results may differ from data you see in the Bing Ads web application
                //Filter = new CampaignPerformanceReportFilter
                //{
                //    DeviceType = DeviceTypeReportFilter.Computer |
                //                 DeviceTypeReportFilter.SmartPhone
                //},

                // Specify the attribute and data report columns. 
                Columns = new[]
                {
                    ProductDimensionPerformanceReportColumn.TimePeriod,
                    ProductDimensionPerformanceReportColumn.MerchantProductId,
                    ProductDimensionPerformanceReportColumn.AccountName,
                    ProductDimensionPerformanceReportColumn.AccountNumber,
                    ProductDimensionPerformanceReportColumn.AdGroupId,
                    ProductDimensionPerformanceReportColumn.AdGroupName,
                    ProductDimensionPerformanceReportColumn.AdId,
                    ProductDimensionPerformanceReportColumn.AverageCpc,
                    ProductDimensionPerformanceReportColumn.Brand,
                    ProductDimensionPerformanceReportColumn.CampaignName,
                    ProductDimensionPerformanceReportColumn.Clicks,
                    ProductDimensionPerformanceReportColumn.Condition,
                    ProductDimensionPerformanceReportColumn.Ctr,
                    ProductDimensionPerformanceReportColumn.CurrencyCode,
                    ProductDimensionPerformanceReportColumn.CustomLabel0,
                    ProductDimensionPerformanceReportColumn.CustomLabel1,
                    ProductDimensionPerformanceReportColumn.CustomLabel2,
                    ProductDimensionPerformanceReportColumn.CustomLabel3,
                    ProductDimensionPerformanceReportColumn.CustomLabel4,
                    ProductDimensionPerformanceReportColumn.DeviceType,
                    ProductDimensionPerformanceReportColumn.Impressions,
                    ProductDimensionPerformanceReportColumn.Language,
                    ProductDimensionPerformanceReportColumn.ProductCategory1,
                    ProductDimensionPerformanceReportColumn.ProductCategory2,
                    ProductDimensionPerformanceReportColumn.ProductCategory3,
                    ProductDimensionPerformanceReportColumn.ProductCategory4,
                    ProductDimensionPerformanceReportColumn.ProductCategory5,
                    ProductDimensionPerformanceReportColumn.ProductType1,
                    ProductDimensionPerformanceReportColumn.ProductType2,
                    ProductDimensionPerformanceReportColumn.ProductType3,
                    ProductDimensionPerformanceReportColumn.ProductType4,
                    ProductDimensionPerformanceReportColumn.ProductType5,
                    ProductDimensionPerformanceReportColumn.Spend,
                    ProductDimensionPerformanceReportColumn.Title,
                    ProductDimensionPerformanceReportColumn.Impressions,
                    ProductDimensionPerformanceReportColumn.Clicks,
                    ProductDimensionPerformanceReportColumn.Ctr,
                    ProductDimensionPerformanceReportColumn.AverageCpc,
                    ProductDimensionPerformanceReportColumn.Spend
                },
            };
        }

        private ReportRequest GetProductPartitionPerformanceReport(long accountId)
        {
            return new ProductPartitionPerformanceReportRequest
            {
                Format = ReportFormat.Tsv,
                ReportName = "My Product Partition Performance Report",
                ReturnOnlyCompleteData = false,
                Aggregation = ReportAggregation.Daily,

                Scope = new AccountThroughAdGroupReportScope
                {
                    AccountIds = new[] { accountId },
                    AdGroups = null,
                    Campaigns = null
                },

                Time = new ReportTime
                {
                    // You may either use a custom date range or predefined time.

                    //CustomDateRangeStart = new Date
                    //    {
                    //        Month = DateTime.Now.Month,
                    //        Day = DateTime.Now.Day,
                    //        Year = DateTime.Now.Year - 1
                    //    },
                    //CustomDateRangeEnd = new Date
                    //    {
                    //    Month = DateTime.Now.Month,
                    //    Day = DateTime.Now.Day,
                    //    Year = DateTime.Now.Year
                    //    },

                    PredefinedTime = ReportTimePeriod.Yesterday
                },

                // If you specify a filter, results may differ from data you see in the Bing Ads web application
                //Filter = new CampaignPerformanceReportFilter
                //{
                //    DeviceType = DeviceTypeReportFilter.Computer |
                //                 DeviceTypeReportFilter.SmartPhone
                //},

                // Specify the attribute and data report columns. 
                Columns = new[]
                {
                    ProductPartitionPerformanceReportColumn.TimePeriod,
                    ProductPartitionPerformanceReportColumn.AccountId,
                    ProductPartitionPerformanceReportColumn.CampaignId,
                    ProductPartitionPerformanceReportColumn.AdGroupCriterionId,
                    ProductPartitionPerformanceReportColumn.ProductGroup,
                    ProductPartitionPerformanceReportColumn.PartitionType,
                    ProductPartitionPerformanceReportColumn.BidMatchType,
                    ProductPartitionPerformanceReportColumn.Clicks,
                    ProductPartitionPerformanceReportColumn.Impressions,
                    ProductPartitionPerformanceReportColumn.Ctr,
                    ProductPartitionPerformanceReportColumn.AverageCpc,
                    ProductPartitionPerformanceReportColumn.Spend,
                },
            };
        }

    }
}

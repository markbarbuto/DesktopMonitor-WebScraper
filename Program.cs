using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;

namespace MonitorScraper
{
    class Program
    {

        public static bool ElementExists(IWebElement element, By by)
        {
            return element.FindElements(by).Count > 0;
        }

        public static void Run()
        {
            var service = ChromeDriverService.CreateDefaultService();
            service.HideCommandPromptWindow = true;

            ChromeOptions options = new ChromeOptions();
            options.AddArgument("headless");
            options.AddArgument("silent");
            options.AddArgument("disable-gpu");
            options.AddArgument("log-level=3");

            // Create browser session reference
            IWebDriver driver = new ChromeDriver(service, options);
            driver.Manage().Cookies.DeleteAllCookies();

            List<string> ASINs = new List<string>();
            DateTime today = DateTime.Now;

            try
            {
                // Get current lowest price
                decimal currentLowest = DBInstance.GetLowestPrice().Item3;

                // Navigate to page
                driver.Navigate().GoToUrl(ConfigurationManager.AppSettings["url"]);

                // get div containing products
                IWebElement products = driver.FindElement(By.XPath(ConfigurationManager.AppSettings["productsPath"]));
                // get all related products (ignore advertised/non-related)
                var ps = products.FindElements(By.XPath(".//div[@data-uuid][@data-asin]"));

                int duplicates = 0;
                int insufficient = 0;
                string ASIN;
                string name;
                decimal basePrice;
                decimal? salePrice;
                bool prime;
                decimal? rating;
                int? reviews;
                string shipping;
                string url;
                foreach (IWebElement p in ps)
                {
                    ASIN = "";
                    name = "";
                    salePrice = null;
                    prime = true;
                    rating = null;
                    reviews = null;
                    shipping = "FREE Shipping by Amazon";
                    url = "";

                    // get ASIN
                    ASIN = p.GetAttribute("data-asin").Trim();

                    // get title & price
                    if (!String.IsNullOrEmpty(ASIN) && ElementExists(p, By.XPath(".//*//h2/a/span")))
                    {
                        url = "amazon.ca/dp/" + ASIN.Trim();

                        Console.WriteLine($"Processing: {ASIN}");
                        Console.WriteLine("Checking if already processed");
                        if (!ASINs.Contains(ASIN))
                        {
                            IWebElement titleElement = p.FindElement(By.XPath(".//*//h2/a/span"));

                            // normalize title
                            name = titleElement.Text.Trim();

                            // check for 1ms and 144hz
                            Console.WriteLine("Checking product specifications");
                            if (new string[] { "1ms", "144hz" }.All(name.ToLower().Replace(" ", "").Contains))
                            {

                                // check for price
                                Console.WriteLine("Checking for price");
                                if (ElementExists(p, By.ClassName("a-price")))
                                {

                                    IWebElement priceElement = p.FindElement(By.ClassName("a-price"));
                                    basePrice = Convert.ToDecimal(priceElement.Text.Trim().Replace("\r\n", ".").Replace("$", ""));

                                    // check for sale price
                                    Console.WriteLine("Checking for sale");
                                    if (ElementExists(p, By.XPath(".//span[@class='a-price a-text-price']")))
                                    {
                                        IWebElement basePriceElement = p.FindElement(By.XPath(".//span[@class='a-price a-text-price']"));
                                        salePrice = basePrice;
                                        basePrice = Convert.ToDecimal(basePriceElement.Text.Trim().Replace("$", ""));
                                    }

                                    // check for prime + shipping
                                    Console.WriteLine("Checking for Prime");
                                    if (!ElementExists(p, By.XPath(".//i[@class='a-icon a-icon-prime a-icon-medium']")) && ElementExists(p, By.XPath(".//div[@class='a-section a-spacing-none a-spacing-top-micro']/div[@class='a-row a-size-base a-color-secondary s-align-children-center']/span[@aria-label]")))
                                    {
                                        IWebElement shippingElement = p.FindElement(By.XPath(".//div[@class='a-section a-spacing-none a-spacing-top-micro']/div[@class='a-row a-size-base a-color-secondary s-align-children-center']/span[@aria-label]"));

                                        shipping = shippingElement.Text.Trim();
                                        prime = false;

                                    }

                                    Console.WriteLine("Checking for rating");
                                    if (ElementExists(p, By.XPath(".//div[@class='a-section a-spacing-none a-spacing-top-micro']/div[@class='a-row a-size-small']/span[1]")))
                                    {
                                        IWebElement ratingElement = p.FindElement(By.XPath(".//div[@class='a-section a-spacing-none a-spacing-top-micro']/div[@class='a-row a-size-small']/span[1]"));

                                        rating = Convert.ToDecimal(ratingElement.GetAttribute("aria-label").Trim().Split(" ").GetValue(0));
                                    }

                                    Console.WriteLine("Checking for reviews");
                                    if (ElementExists(p, By.XPath(".//div[@class='a-section a-spacing-none a-spacing-top-micro']/div[@class='a-row a-size-small']/span[2]")))
                                    {
                                        IWebElement reviewElement = p.FindElement(By.XPath(".//div[@class='a-section a-spacing-none a-spacing-top-micro']/div[@class='a-row a-size-small']/span[2]/a/span"));

                                        reviews = Convert.ToInt32(reviewElement.Text.Replace(",", "").Trim());
                                    }

                                    Console.WriteLine("OK");
                                    ASINs.Add(ASIN);
                                    DBInstance.Insert(ASIN, name, salePrice, basePrice, prime, rating, reviews, shipping, url);

                                }
                                else
                                {
                                    Console.WriteLine("No price found");
                                    insufficient++;
                                }
                            }
                            else
                            {
                                Console.WriteLine("Does not have required specifications");
                                insufficient++;
                            }
                        }
                        else
                        {
                            Console.WriteLine("Found duplicate");
                            duplicates++;
                        }

                        Console.WriteLine("");
                    }

                }

                Console.WriteLine($"Found ({ps.Count}) products");
                Console.WriteLine($"({duplicates}) duplicates");
                Console.WriteLine($"({insufficient}) with insufficient information");
                Console.WriteLine($"({ASINs.Count}) inserted");

                // Check for new lowest price
                Console.WriteLine("\nComparing lowest price");
                if (DBInstance.GetLowestPrice().Item3 < currentLowest)
                {
                    Console.WriteLine("Found lower price, notifying...");
                    Mailer.Notify();
                }
                //else
                Console.WriteLine("No lower price found");

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                Mailer.SendError(e.ToString());
            }
            finally
            {
                Console.WriteLine("\nFinished.");
                driver.Quit();
            }
        }

        static void Main(string[] args)
        {
            using (StreamWriter writer = new StreamWriter($@"C:\Users\markb\source\repos\MonitorScraper\log\log-{DateTime.Now:yyyy-MM-dd}.txt"))
            {
                Console.SetOut(writer);
                Run();
            }

        }
    }
}

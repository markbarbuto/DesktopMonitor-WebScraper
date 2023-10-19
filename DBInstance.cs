using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Text;

namespace MonitorScraper
{
    static class DBInstance
    {

        public static void Insert(string ASIN, string name, decimal? salePrice, decimal basePrice, bool prime, decimal? rating, int? reviews, string shipping, string URL)
        {
            try
            {
                if (ProductExists(ASIN))
                    UpdateMonitor(ASIN, name, salePrice, basePrice, prime, rating, reviews, shipping);
                else
                    InsertMonitor(ASIN, name, salePrice, basePrice, prime, rating, reviews, shipping, URL);
                InsertListing(ASIN, salePrice, basePrice, prime, shipping);
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        public static bool ProductExists(string ASIN)
        {
            try
            {
                string query = "SELECT COUNT(*) FROM Monitor WHERE ASIN=@ASIN";
                using SqlConnection connection = new SqlConnection(ConfigurationManager.ConnectionStrings["scraperDB"].ConnectionString);
                connection.Open();

                using SqlCommand cmd = new SqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@ASIN", ASIN);
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        public static void InsertMonitor(string ASIN, string name, decimal? salePrice, decimal basePrice, bool prime, decimal? rating, int? reviews, string shipping, string URL)
        {
            try
            {
                string query = "INSERT INTO Monitor (ASIN, Name, CurrentPrice, BasePrice, LowestPrice, Prime, Rating, Reviews, Shipping, URL, DateModified) " +
                                 "VALUES(@ASIN, @Name, @CurrentPrice, @BasePrice, @LowestPrice, @Prime, @Rating, @Reviews, @Shipping, @URL, getdate())";
                using SqlConnection connection = new SqlConnection(ConfigurationManager.ConnectionStrings["scraperDB"].ConnectionString);
                connection.Open();

                using SqlCommand cmd = new SqlCommand(query, connection);
                
                cmd.Parameters.AddWithValue("@ASIN", ASIN);
                cmd.Parameters.AddWithValue("@Name", name);

                if (salePrice == null)
                {
                    cmd.Parameters.AddWithValue("@CurrentPrice", basePrice);
                    cmd.Parameters.AddWithValue("@LowestPrice", basePrice);
                }
                else
                {
                    cmd.Parameters.AddWithValue("@CurrentPrice", salePrice);
                    cmd.Parameters.AddWithValue("@LowestPrice", salePrice);
                }

                cmd.Parameters.AddWithValue("@BasePrice", basePrice);
                cmd.Parameters.AddWithValue("@Prime", prime);

                if (rating == null)
                    cmd.Parameters.AddWithValue("@Rating", DBNull.Value);
                else
                    cmd.Parameters.AddWithValue("@Rating", rating);

                if (reviews == null)
                    cmd.Parameters.AddWithValue("@Reviews", DBNull.Value);
                else
                    cmd.Parameters.AddWithValue("@Reviews", reviews);

                cmd.Parameters.AddWithValue("@Shipping", shipping);
                cmd.Parameters.AddWithValue("@URL", URL);

                int RowsAffected = cmd.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                throw e;
            }
        }
        
        private static void UpdateMonitor(string ASIN, string name, decimal? salePrice, decimal basePrice, bool prime, decimal? rating, int? reviews, string shipping)
        {
            try
            {
                decimal lowestPrice;
                if (salePrice.HasValue)
                    lowestPrice = Math.Min(GetLowestPriceByASIN(ASIN), salePrice.Value);
                else
                    lowestPrice = Math.Min(GetLowestPriceByASIN(ASIN), basePrice);

                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.Append("UPDATE Monitor SET ");
                stringBuilder.Append("Name=@Name, ");
                stringBuilder.Append("CurrentPrice=@CurrentPrice, ");
                stringBuilder.Append("BasePrice=@BasePrice, ");
                stringBuilder.Append("LowestPrice=@LowestPrice, ");
                stringBuilder.Append("Prime=@Prime, ");
                stringBuilder.Append("Shipping=@Shipping, ");
                stringBuilder.Append("DateModified=getdate() ");

                string query = stringBuilder.ToString();

                using SqlConnection connection = new SqlConnection(ConfigurationManager.ConnectionStrings["scraperDB"].ConnectionString);
                connection.Open();

                using SqlCommand cmd = new SqlCommand();

                cmd.Parameters.AddWithValue("@ASIN", ASIN);
                cmd.Parameters.AddWithValue("@Name", name);

                if (!salePrice.HasValue)
                    cmd.Parameters.AddWithValue("@CurrentPrice", basePrice);
                else
                    cmd.Parameters.AddWithValue("@CurrentPrice", salePrice);

                cmd.Parameters.AddWithValue("@BasePrice", basePrice);
                cmd.Parameters.AddWithValue("@LowestPrice", lowestPrice);
                cmd.Parameters.AddWithValue("@Prime", prime);

                if (rating.HasValue)
                {
                    stringBuilder.Append(", Rating=@Rating ");
                    cmd.Parameters.AddWithValue("@Rating", rating);
                }
                if (reviews.HasValue)
                {
                    stringBuilder.Append(", Reviews=@Reviews ");
                    cmd.Parameters.AddWithValue("@Reviews", reviews);
                }

                cmd.Parameters.AddWithValue("@Shipping", shipping);

                stringBuilder.Append("WHERE ASIN=@ASIN");


                cmd.CommandText = stringBuilder.ToString();
                cmd.Connection = connection;

                int RowsAffected = cmd.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                throw e;
            }
        }
        
        public static void InsertListing(string ASIN, decimal? salePrice, decimal basePrice, bool prime, string shipping)
        {
            try
            {
                string query = "INSERT INTO MonitorListings (ASIN, SalePrice, BasePrice, Prime, Shipping, Date) " +
                                 "VALUES(@ASIN, @SalePrice, @BasePrice, @Prime, @Shipping, getdate())";

                using SqlConnection connection = new SqlConnection(ConfigurationManager.ConnectionStrings["scraperDB"].ConnectionString);
                connection.Open();

                using SqlCommand cmd = new SqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@ASIN", ASIN);

                if (salePrice == null)
                    cmd.Parameters.AddWithValue("@SalePrice", DBNull.Value);
                else
                    cmd.Parameters.AddWithValue("@SalePrice", salePrice);

                cmd.Parameters.AddWithValue("@BasePrice", basePrice);
                cmd.Parameters.AddWithValue("@Prime", prime);
                cmd.Parameters.AddWithValue("@Shipping", shipping);

                int RowsAffected = cmd.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        public static Tuple<string, string, decimal> GetLowestPrice()
        {
            try
            {
                string query = "SELECT Name, URL, MIN(CurrentPrice) As BestPrice " +
                               "FROM Monitor " +
                               "GROUP BY ASIN, Name, URL " +
                               "ORDER BY BestPrice ASC;";

                using SqlConnection connection = new SqlConnection(ConfigurationManager.ConnectionStrings["scraperDB"].ConnectionString);
                connection.Open();

                using SqlCommand cmd = new SqlCommand(query, connection);
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            return new Tuple<string, string, decimal>(reader[0].ToString(), reader[1].ToString(), Convert.ToDecimal(reader[2]));
                        }

                    }
                    return new Tuple<string, string, decimal>("Not found", "Not found", -1);
                }
                //return Convert.ToDecimal(cmd.ExecuteScalar());
            }
            catch (Exception e)
            {
                throw e;
            }
        }
        
        public static decimal GetLowestPriceByASIN(string ASIN)
        {
            try
            {
                string query = "SELECT LowestPrice FROM Monitor WHERE ASIN=@ASIN";

                using SqlConnection connection = new SqlConnection(ConfigurationManager.ConnectionStrings["scraperDB"].ConnectionString);
                connection.Open();

                using SqlCommand cmd = new SqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@ASIN", ASIN);

                return Convert.ToDecimal(cmd.ExecuteScalar());
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        public static void SelectByDate(string date)
        {
            try
            {
                string query = "SELECT TOP 3 ASIN, Name, MIN(SalePrice) As Price, URL " +
                               "FROM Monitors WHERE " +
                               "FORMAT(Date, 'yyyy-MM-dd')=@Date " + 
                               "GROUP BY ASIN, Name, URL " +
                               "ORDER BY Price ASC";

                using SqlConnection connection = new SqlConnection(ConfigurationManager.ConnectionStrings["scraperDB"].ConnectionString);
                connection.Open();

                using SqlCommand cmd = new SqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@Date", date);
                using SqlDataReader reader = cmd.ExecuteReader();
                if (reader.HasRows)
                {
                    while (reader.Read())
                    {
                        Console.WriteLine(String.Format("{0}, {1}, {2}",
                            reader[1], reader[2], reader[3]));
                    }

                }
            }
            catch (Exception e)
            {
                throw e;
            }
        }
        
        public static void SelectBest()
        {
            try
            {
                string query = "SELECT TOP 3 ASIN, MIN(SalePrice) As Price, Name, URL " +
                               "FROM Monitors " +
                               "GROUP BY ASIN, Name, URL " +
                               "ORDER BY Price ASC;";
                using (SqlConnection connection = new SqlConnection(ConfigurationManager.ConnectionStrings["scraperDB"].ConnectionString))
                {
                    connection.Open();

                    using (SqlCommand cmd = new SqlCommand(query, connection))
                    {
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                while (reader.Read())
                                {
                                    Console.WriteLine(String.Format("{0}, {1}, {2}",
                                        reader[1], reader[2], reader[3]));
                                }

                            }
                        }

                    }
                }
            }
            catch (Exception e)
            {
                throw e;
            }
        }

    }
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Graph;

namespace b2c_ms_graph
{
    class UserService
    {
        static public FileStream ostrm;
        static public StreamWriter writer;
        static public TextWriter oldOut = Console.Out;

        //<ms_docref_get_list_of_user_accounts>
        public static async Task ListUsers(GraphServiceClient graphClient)
        {
            // Change the name of the file here
            
            try
            {
                ostrm = new FileStream("./AllUsers.json", FileMode.OpenOrCreate, FileAccess.Write);
                writer = new StreamWriter(ostrm);
            }

            catch (Exception e)
            {
                Console.WriteLine("Cannot open Redirect.txt for writing");
                Console.WriteLine(e.Message);
                return;
            }

            Console.WriteLine("Getting list of users...");
            Console.SetOut(writer);

            var options = new JsonSerializerOptions { WriteIndented = true };
            
            try
            {
                // Get all users
                var users = await graphClient.Users
                    .Request()
                    .Select(e => new
                    {
                        e.DisplayName,
                        e.Id,
                        e.Identities,
                        e.AccountEnabled,
                        e.CreatedDateTime,
                        e.GivenName,
                        e.Mail,
                        e.OtherMails,
                        e.UserPrincipalName,
                        e.UserType,
                        //e.Birthday,
                        e.Authentication,
                        e.City,
                        e.CompanyName,
                        e.Country,
                        e.Department,
                        e.EmployeeHireDate,
                        e.EmployeeId,
                        //e.HireDate,
                        //e.OfficeLocation,  
                        e.PasswordPolicies,
                        e.People,
                        e.PostalCode,
                        //e.PreferredName,
                        e.State,
                        e.StreetAddress,
                        e.Surname,                        
                    })
                    .GetAsync();

                // Iterate over all the users in the directory
                var pageIterator = PageIterator<User>
                    .CreatePageIterator(
                        graphClient,
                        users,
                        // Callback executed for each user in the collection
                        (user) =>
                        {
                            string jsonString = JsonSerializer.Serialize(user, options);

                            Console.WriteLine(jsonString);

                            // Console.WriteLine(JsonSerializer.Serialize(user), options);
                            return true;                            
                        },
                        // Used to configure subsequent page requests
                        (req) =>
                        {
                            Console.WriteLine($"Reading next page of users...");
                            return req;
                        }
                    );

                await pageIterator.IterateAsync();
            }
            
            catch (Exception ex)
            {
                Console.SetOut(UserService.oldOut);
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(ex.Message);
                Console.ResetColor();
            }

            writer.Close();
            ostrm.Close();
        }
        //</ms_docref_get_list_of_user_accounts>

        public static async Task CountUsers(GraphServiceClient graphClient)
        {
            int i = 0;
            Console.WriteLine("Getting list of users...");

            try
            {
                // Get all users 
                var users = await graphClient.Users
                    .Request()
                    .Select(e => new
                    {
                        e.DisplayName,
                        e.Id,
                        e.Identities
                    })
                    .GetAsync();

                // Iterate over all the users in the directory
                var pageIterator = PageIterator<User>
                    .CreatePageIterator(
                        graphClient,
                        users,
                        // Callback executed for each user in the collection
                        (user) =>
                        {
                            i += 1;
                            return true;
                        },
                        // Used to configure subsequent page requests
                        (req) =>
                        {
                            Console.WriteLine($"Reading next page of users. Number of users: {i}");
                            return req;
                        }
                    );

                await pageIterator.IterateAsync();

                Console.WriteLine("========================");
                Console.WriteLine($"Number of users in the directory: {i}");
                Console.WriteLine("========================");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(ex.Message);
                Console.ResetColor();
            }
        }
        public static async Task ListUsersWithCustomAttribute(GraphServiceClient graphClient, string b2cExtensionAppClientId)
        {
            if (string.IsNullOrWhiteSpace(b2cExtensionAppClientId))
            {
                throw new ArgumentException("B2cExtensionAppClientId (its Application ID) is missing from appsettings.json. Find it in the App registrations pane in the Azure portal. The app registration has the name 'b2c-extensions-app. Do not modify. Used by AADB2C for storing user data.'.", nameof(b2cExtensionAppClientId));
            }

            // Declare the names of the custom attributes
            const string customAttributeName1 = "FavouriteSeason";
            const string customAttributeName2 = "LovesPets";

            // Get the complete name of the custom attribute (Azure AD extension)
            Helpers.B2cCustomAttributeHelper helper = new Helpers.B2cCustomAttributeHelper(b2cExtensionAppClientId);
            string favouriteSeasonAttributeName = helper.GetCompleteAttributeName(customAttributeName1);
            string lovesPetsAttributeName = helper.GetCompleteAttributeName(customAttributeName2);

            Console.WriteLine($"Getting list of users with the custom attributes '{customAttributeName1}' (string) and '{customAttributeName2}' (boolean)");
            Console.WriteLine();

            // Get all users (one page)
            var result = await graphClient.Users
                .Request()
                .Select($"id,displayName,identities,{favouriteSeasonAttributeName},{lovesPetsAttributeName}")
                .GetAsync();

            foreach (var user in result.CurrentPage)
            {
                Console.WriteLine(JsonSerializer.Serialize(user));

                // Only output the custom attributes...
                //Console.WriteLine(JsonSerializer.Serialize(user.AdditionalData));
            }
        }

        public static async Task GetUserById(GraphServiceClient graphClient)
        {
            Console.Write("Enter user object ID: ");
            string userId = Console.ReadLine();

            Console.WriteLine($"Looking for user with object ID '{userId}'...");

            try
            {
                // Get user by object ID
                var result = await graphClient.Users[userId]
                    .Request()
                    .Select(e => new
                    {
                        e.DisplayName,
                        e.Id,
                        e.Identities
                    })
                    .GetAsync();

                if (result != null)
                {
                    Console.WriteLine(JsonSerializer.Serialize(result));
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(ex.Message);
                Console.ResetColor();
            }
        }

        public static async Task GetUserBySignInName(AppSettings config, GraphServiceClient graphClient)
        {
            Console.Write("Enter user sign-in name (username or email address): ");
            string userId = Console.ReadLine();

            Console.WriteLine($"Looking for user with sign-in name '{userId}'...");

            try
            {
                // Get user by sign-in name
                var result = await graphClient.Users
                    .Request()
                    .Filter($"identities/any(c:c/issuerAssignedId eq '{userId}' and c/issuer eq '{config.TenantId}')")
                    .Select(e => new
                    {
                        e.DisplayName,
                        e.Id,
                        e.Identities
                    })
                    .GetAsync();

                if (result != null)
                {
                    Console.WriteLine(JsonSerializer.Serialize(result));
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(ex.Message);
                Console.ResetColor();
            }
        }

        public static async Task DeleteUserById(GraphServiceClient graphClient)
        {
            Console.Write("Enter user object ID: ");
            string userId = Console.ReadLine();

            Console.WriteLine($"Looking for user with object ID '{userId}'...");

            try
            {
                // Delete user by object ID
                await graphClient.Users[userId]
                   .Request()
                   .DeleteAsync();

                Console.WriteLine($"User with object ID '{userId}' successfully deleted.");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(ex.Message);
                Console.ResetColor();
            }
        }

        public static async Task SetPasswordByUserId(GraphServiceClient graphClient)
        {
            Console.Write("Enter user object ID: ");
            string userId = Console.ReadLine();

            Console.Write("Enter new password: ");
            string password = Console.ReadLine();

            Console.WriteLine($"Looking for user with object ID '{userId}'...");

            var user = new User
            {
                PasswordPolicies = "DisablePasswordExpiration,DisableStrongPassword",
                PasswordProfile = new PasswordProfile
                {
                    ForceChangePasswordNextSignIn = false,
                    Password = password,
                }
            };

            try
            {
                // Update user by object ID
                await graphClient.Users[userId]
                   .Request()
                   .UpdateAsync(user);

                Console.WriteLine($"User with object ID '{userId}' successfully updated.");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(ex.Message);
                Console.ResetColor();
            }
        }

        public static async Task BulkCreate(AppSettings config, GraphServiceClient graphClient)
        {
            // Get the users to import
            string appDirectoryPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string dataFilePath = Path.Combine(appDirectoryPath, config.UsersFileName);

            // Verify and notify on file existence
            if (!System.IO.File.Exists(dataFilePath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"File '{dataFilePath}' not found.");
                Console.ResetColor();
                Console.ReadLine();
                return;
            }

            Console.WriteLine("Starting bulk create operation...");

            // Read the data file and convert to object
            UsersModel users = UsersModel.Parse(System.IO.File.ReadAllText(dataFilePath));

            foreach (var user in users.Users)
            {
                user.SetB2CProfile(config.TenantId);

                try
                {
                    // Create the user account in the directory
                    User user1 = await graphClient.Users
                                    .Request()
                                    .AddAsync(user);

                    Console.WriteLine($"User '{user.DisplayName}' successfully created.");
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(ex.Message);
                    Console.ResetColor();
                }
            }
        }

        public static async Task CreateUserWithCustomAttribute(GraphServiceClient graphClient, string b2cExtensionAppClientId, string tenantId)
        {
            if (string.IsNullOrWhiteSpace(b2cExtensionAppClientId))
            {
                throw new ArgumentException("B2C Extension App ClientId (ApplicationId) is missing in the appsettings.json. Get it from the App Registrations blade in the Azure portal. The app registration has the name 'b2c-extensions-app. Do not modify. Used by AADB2C for storing user data.'.", nameof(b2cExtensionAppClientId));
            }

            // Declare the names of the custom attributes
            const string customAttributeName1 = "FavouriteSeason";
            const string customAttributeName2 = "LovesPets";

            // Get the complete name of the custom attribute (Azure AD extension)
            Helpers.B2cCustomAttributeHelper helper = new Helpers.B2cCustomAttributeHelper(b2cExtensionAppClientId);
            string favouriteSeasonAttributeName = helper.GetCompleteAttributeName(customAttributeName1);
            string lovesPetsAttributeName = helper.GetCompleteAttributeName(customAttributeName2);

            Console.WriteLine($"Create a user with the custom attributes '{customAttributeName1}' (string) and '{customAttributeName2}' (boolean)");

            // Fill custom attributes
            IDictionary<string, object> extensionInstance = new Dictionary<string, object>();
            extensionInstance.Add(favouriteSeasonAttributeName, "summer");
            extensionInstance.Add(lovesPetsAttributeName, true);

            try
            {
                // Create user
                var result = await graphClient.Users
                .Request()
                .AddAsync(new User
                {
                    GivenName = "Casey",
                    Surname = "Jensen",
                    DisplayName = "Casey Jensen",
                    Identities = new List<ObjectIdentity>
                    {
                        new ObjectIdentity()
                        {
                            SignInType = "emailAddress",
                            Issuer = tenantId,
                            IssuerAssignedId = "casey.jensen@example.com"
                        }
                    },
                    PasswordProfile = new PasswordProfile()
                    {
                        Password = Helpers.PasswordHelper.GenerateNewPassword(4, 8, 4)
                    },
                    PasswordPolicies = "DisablePasswordExpiration",
                    AdditionalData = extensionInstance
                });

                string userId = result.Id;

                Console.WriteLine($"Created the new user. Now get the created user with object ID '{userId}'...");

                // Get created user by object ID
                result = await graphClient.Users[userId]
                    .Request()
                    .Select($"id,givenName,surName,displayName,identities,{favouriteSeasonAttributeName},{lovesPetsAttributeName}")
                    .GetAsync();

                if (result != null)
                {
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.WriteLine($"DisplayName: {result.DisplayName}");
                    Console.WriteLine($"{customAttributeName1}: {result.AdditionalData[favouriteSeasonAttributeName].ToString()}");
                    Console.WriteLine($"{customAttributeName2}: {result.AdditionalData[lovesPetsAttributeName].ToString()}");
                    Console.WriteLine();
                    Console.ResetColor();
                    Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
                }
            }
            catch (ServiceException ex)
            {
                if (ex.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Have you created the custom attributes '{customAttributeName1}' (string) and '{customAttributeName2}' (boolean) in your tenant?");
                    Console.WriteLine();
                    Console.WriteLine(ex.Message);
                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(ex.Message);
                Console.ResetColor();
            }
        }
    }
}

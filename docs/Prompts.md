Prompts

remove all code related to test cases. I will give fresh UI test case. 
Project: Confessions tracking using Admin Portal
URL: https://maidencube.com/cube-admin-prod/
Test Scenario: Test Authentication with credentials admin@confess.com, Admin@123

Why are you hardcoding test case/scenario in MockLLMService. We have to create document manager to User Story. You have to create test case dynamically and not hardcoded which is based on uploaded user story


Why do you hardcode below lines in program.cs. User should be asked to upload user story or read confessions_portal_auth.txt file from docs folder and upload to document manager and then start testing dynamically
userStory = @"Test Authentication with credentials admin@confess.com, Admin@123 
        for Confessions tracking Admin Portal at https://maidencube.com/cube-admin-prod/";
projectContext = "Confessions tracking Admin Portal - Authentication and access control system";
Console.WriteLine("Using demo story: Confessions Portal Authentication Test");
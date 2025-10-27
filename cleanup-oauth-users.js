// MongoDB script to clean up OAuth users with empty emails
// Run this script in your MongoDB shell or using mongosh:
// mongosh <connection-string> cleanup-oauth-users.js

// Switch to your database (adjust the database name if needed)
db = db.getSiblingDB('wishera');

print('=== OAuth User Cleanup Script ===\n');

// Find users with empty or missing email
print('Finding users with empty emails...');
const usersWithEmptyEmail = db.users.find({
    $or: [
        { email: '' },
        { email: { $exists: false } },
        { emailNormalized: '' },
        { emailNormalized: { $exists: false } }
    ]
}).toArray();

print(`Found ${usersWithEmptyEmail.length} users with empty/missing emails:\n`);
usersWithEmptyEmail.forEach(user => {
    print(`  - ID: ${user._id}, Username: ${user.username}, Email: '${user.email || 'N/A'}', GoogleId: '${user.googleId || 'N/A'}'`);
});

// Find users with OAuth (no password) but no GoogleId
print('\nFinding OAuth users without GoogleId...');
const oauthUsersWithoutGoogleId = db.users.find({
    passwordHash: '',
    $or: [
        { googleId: '' },
        { googleId: { $exists: false } },
        { googleId: null }
    ]
}).toArray();

print(`Found ${oauthUsersWithoutGoogleId.length} OAuth users without GoogleId:\n`);
oauthUsersWithoutGoogleId.forEach(user => {
    print(`  - ID: ${user._id}, Username: ${user.username}, Email: '${user.email || 'N/A'}'`);
});

// Ask for confirmation before deletion (comment out the confirmation if running in a script)
print('\n=== Cleanup Actions ===');
print('This script will:');
print('1. DELETE users with empty emails AND no password (orphaned OAuth users)');
print('2. Keep users with empty emails but WITH passwords (regular users)');
print('\nTo proceed, uncomment the cleanup code below in the script.\n');

// UNCOMMENT THE CODE BELOW TO PERFORM THE CLEANUP:
/*
print('Starting cleanup...\n');

// Delete OAuth users with empty email (they're broken)
const deleteResult = db.users.deleteMany({
    passwordHash: '',  // OAuth users have no password
    $or: [
        { email: '' },
        { emailNormalized: '' }
    ]
});

print(`✓ Deleted ${deleteResult.deletedCount} orphaned OAuth users with empty emails`);

// Update remaining OAuth users to ensure they have the googleId field
const updateResult = db.users.updateMany(
    {
        passwordHash: '',
        $or: [
            { googleId: { $exists: false } },
            { googleId: null }
        ]
    },
    {
        $set: { googleId: '' }
    }
);

print(`✓ Updated ${updateResult.modifiedCount} OAuth users to add googleId field`);
print('\nCleanup complete!');
*/

print('\n=== Instructions ===');
print('1. Review the users listed above');
print('2. Uncomment the cleanup code in this script if you want to delete them');
print('3. Restart your auth service to apply the new OAuth validation');
print('4. Test logging in with Google OAuth again');
print('5. Check the console logs for [OAuth Debug] messages');



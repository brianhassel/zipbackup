# zipbackup

ZipBackup
I needed a way to perform backups of my personal computers as well as a few server machines. In addition to a simple, local backup, I wanted the ability for the backups to be sent off-site to a separate storage location. Cloud storage supporting FTP is super cheap at places like GoDaddy, so this seemed like a good choice to transfer the files. Now, I also required that the backups be protected with strong encryption, as I don't like the idea of fulling trusting off-site storage without an encryption method that I control. Dropbox and similar services where the encryption is done with master keys are inherently insecure -- not acceptable.

Now, one would think it would be easy to find a freeware/shareware solution that has the features I listed above. After trying about a dozen different options, I didn't find what I was looking for. This drove me to assemble a solution where I would not have to compromise on any of the features I wanted.

At its core, ZipBackup uses the command line version of 7Zip to generate archive files. The 7Zip compression format is fast, efficient, supports AES256 encryption, and is free (LGPL). ZipBackup uses the Process object in .Net to send commands to the 7Zip program in order to build archives containing the desired directories and files.

Another benefit to using 7Zip as the archive format is its support for incremental backup files. That is, a 'full' backup file can be made containing all of the desired files and then subsequent 'incremental' backup files can be made that only contain items that have changed since the last full backup. Considering ZipBackup is designed to interface with a off-site storage location via FTP, minimizing the amount of data transmitted is required. By performing a full backup infrequently (e.g. every 60 days) but daily incremental backups of the changed files, the amount of data sent to the off-site location is much less than if the full backup was performed every day.

Some other feature of ZipBackup (will be added to as I continue to enhance the program): 
Full FTP mirroring of all backup files (local to remote).
FTP over SSL supported.
All passwords are stored using Windows DPAPI functions. (no plain-text passwords in config files)
Configurable full-backup interval and the number of incremental backups to retain.
Full logging using the fantastic NLog project.
Email sent to inform of success/failure. Log of operations included.
Sleep mode suspended during backup operations.
Though this is a work in progress, I am currently using it.


I release this under the GNU General Public License 3.0 

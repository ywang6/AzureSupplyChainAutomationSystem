$(document).ready(function () {
    $('#btnUpload').click(function () {
        var fileUploadUrl = $('#FileUploadUrl').val();
        var files = new FormData();
        var file1 = document.getElementById("fileOne").files[0];
        files.append('files[0]', file1);

        $.ajax({
            type: 'POST',
            url: fileUploadUrl,
            data: files,
            dataType: 'json',
            cache: false,
            contentType: false,
            processData: false,
            success: function (response) {
                $('#uploadMsg').text('Files have been uploaded successfully');
            },
            error: function (error) {
                $('#uploadMsg').text('Error has occured. Upload is failed');
            }
        });
    });
});  
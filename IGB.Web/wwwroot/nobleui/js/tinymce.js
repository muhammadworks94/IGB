// npm package: tinymce
// github link: https://github.com/tinymce/tinymce

document.addEventListener('DOMContentLoaded', function() {
  'use strict';

  //Tinymce editor
  var tinymceExample = document.getElementById('tinymceExample');
  if (tinymceExample) {
    tinymce.init({
      selector: '#tinymceExample',
      min_height: 350,
      default_text_color: 'red',
      plugins: [
        'advlist', 'autoresize', 'autolink', 'lists', 'link', 'image', 'charmap', 'preview', 'anchor', 'pagebreak',
        'searchreplace', 'wordcount', 'visualblocks', 'visualchars', 'code', 'fullscreen',
      ],
      toolbar1: 'undo redo | insert | styleselect | bold italic | alignleft aligncenter alignright alignjustify | bullist numlist outdent indent | link image',
      toolbar2: 'print preview media | forecolor backcolor emoticons | codesample help',
      image_advtab: true,
      templates: [{
          title: 'Test template 1',
          content: 'Test 1'
        },
        {
          title: 'Test template 2',
          content: 'Test 2'
        }
      ],
      content_css: []
    });
  }

});
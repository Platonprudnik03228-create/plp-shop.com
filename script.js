// PLP script.js
document.addEventListener('DOMContentLoaded', () => {
  const observer = new IntersectionObserver((entries)=>{
    entries.forEach(entry=>{
      if(entry.isIntersecting) entry.target.classList.add('visible');
    });
  }, {threshold:0.15});

  document.querySelectorAll('.section,.card,.product').forEach(el=>observer.observe(el));

  document.querySelectorAll('a[href^="#"]').forEach(link=>{
    link.addEventListener('click', e=>{
      const id = link.getAttribute('href');
      if(id.length>1){
        const target = document.querySelector(id);
        if(target){
          e.preventDefault();
          target.scrollIntoView({behavior:'smooth'});
        }
      }
    });
  });

  document.querySelectorAll('.card,.product').forEach(card=>{
    card.addEventListener('mousemove', e=>{
      const r = card.getBoundingClientRect();
      const x = e.clientX - r.left;
      const y = e.clientY - r.top;
      card.style.background =
        `radial-gradient(circle at ${x}px ${y}px, rgba(255,0,0,.12), #141414 60%)`;
    });

    card.addEventListener('mouseleave', ()=>{
      card.style.background = '#141414';
    });
  });
});
